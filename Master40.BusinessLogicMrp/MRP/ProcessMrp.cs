﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Master40.DB.Data.Context;
using Master40.DB.Models;
using Microsoft.EntityFrameworkCore;
using Master40.BusinessLogicCentral.HelperCapacityPlanning;
using Master40.DB.Enums;
using Master40.MessageSystem.Messages;
using Master40.MessageSystem.SignalR;
using System;
using Remotion.Linq.Parsing.Structure.IntermediateModel;

namespace Master40.BusinessLogicCentral.MRP
{
    public interface IProcessMrp
    {
        Task CreateAndProcessOrderDemand(MrpTask task);
        void RunRequirementsAndTermination(IDemandToProvider demand, MrpTask task);
        void PlanCapacities(MrpTask task, bool newOrdersAdded);
        void UpdateDemandsAndOrders();
    }

    public class ProcessMrp : IProcessMrp
    {
        private readonly IRebuildNets _rebuildNets;
        private readonly IMessageHub _messageHub;
        private readonly ProductionDomainContext _context;
        private readonly IScheduling _scheduling;
        private readonly IDemandForecast _demandForecast;
        private readonly ICapacityScheduling _capacityScheduling;
        public ProcessMrp(ProductionDomainContext context, IScheduling scheduling, ICapacityScheduling capacityScheduling, IMessageHub messageHub, IRebuildNets rebuildNets)
        {
            _messageHub = messageHub;
            _context = context;
            _scheduling = scheduling;
            _capacityScheduling = capacityScheduling;
            _demandForecast = new DemandForecast(context,this);
            _rebuildNets = rebuildNets;
        }

        /// <summary>
        /// Plans all unplanned Orders with MRP I + II
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        public async Task CreateAndProcessOrderDemand(MrpTask task)
        {
            await Task.Run(() =>
            {
                var newOrdersAdded = false;
                _messageHub.SendToAllClients("Start full MRP cycle...", MessageType.info);

                //get all unplanned orderparts and iterate through them for MRP
                var maxAllowedTime = _context.SimulationConfigurations.Last().Time +
                                     _context.SimulationConfigurations.Last().MaxCalculationTime;
                var orderParts = _context.OrderParts.Where(a => a.IsPlanned == false && a.Order.DueTime < maxAllowedTime).Include(a => a.Article).ToList();
                if (orderParts.Any()) newOrdersAdded = true;
                foreach (var orderPart in orderParts.ToList())
                {
                    var demand = GetDemand(orderPart);
                    //run the requirements planning and backward/forward termination algorithm
                    RunRequirementsAndTermination(demand, task);
                }
                
                if (task == MrpTask.All || task == MrpTask.GifflerThompson || task == MrpTask.Capacity)
                {
                    //run the capacity algorithm
                    PlanCapacities(task, newOrdersAdded);
                    
                    _messageHub.SendToAllClients("Capacities are planned");
                }
                //set all orderparts to be planned
                foreach (var orderPart in orderParts)
                {
                    if (task == MrpTask.All || task == MrpTask.GifflerThompson)
                        orderPart.IsPlanned = true;
                }
                _context.SaveChanges();
                _messageHub.SendToAllClients("End of the latest calculated order: "+ _context.ProductionOrderWorkSchedules.Max(a => a.End));
            });
            
        }

        /// <summary>
        /// Check if a demand exists for the orderpart, else a new one is created.
        /// </summary>
        /// <param name="orderPart"></param>
        /// <returns></returns>
        private IDemandToProvider GetDemand(OrderPart orderPart)
        {
            var demandOrderParts =
                        _context.Demands.OfType<DemandOrderPart>().Include(a => a.DemandProvider).Where(a => a.OrderPartId == orderPart.Id).ToList();
            IDemandToProvider demand;
            if (demandOrderParts.Any())
                demand = demandOrderParts.First();
            else
            {
                demand = _context.CreateDemandOrderPart(orderPart);
                _context.SaveChanges();
            }
            return demand;
        }

        /// <summary>
        /// Plans capacities for all demands which are either already planned or just finished with MRP.
        /// </summary>
        /// <param name="task"></param>
        /// <param name="newOrdersAdded"></param>
        public void PlanCapacities(MrpTask task, bool newOrdersAdded)
        {
            var timer = _context.SimulationConfigurations.Last().Time;
            var demands = _context.Demands.Where(a =>
                               a.State == State.BackwardScheduleExists || 
                               a.State == State.ForwardScheduleExists ||
                               a.State == State.ExistsInCapacityPlan ||
                               (timer > 0 && a.State == State.Injected)).ToList();
           
            List<MachineGroupProductionOrderWorkSchedule> machineList = null;

            if (newOrdersAdded)
            {
                _rebuildNets.Rebuild();
                _messageHub.SendToAllClients("RebuildNets completed");
            }

            if (timer == 0 && (task == MrpTask.All || task == MrpTask.Capacity))
                //creates a list with the needed capacities to follow the terminated schedules
                machineList = _capacityScheduling.CapacityRequirementsPlanning();
            
            if (timer != 0 || (task == MrpTask.GifflerThompson || (_capacityScheduling.CapacityLevelingCheck(machineList) && task == MrpTask.All)))
                _capacityScheduling.GifflerThompsonScheduling();
            else
            {
                foreach (var demand in demands)
                    SetStartEndFromTermination(demand);
                
                _capacityScheduling.SetMachines();
            }
            foreach (var demand in demands)
            {
                demand.State = State.ExistsInCapacityPlan;
            }
            _context.Demands.UpdateRange(demands);
            _context.SaveChanges();
        }
        
        private void SetStartEndFromTermination(IDemandToProvider demand)
        { //Todo: replace provider.first()
            var schedules = _context.ProductionOrderWorkSchedules
                .Include(a => a.ProductionOrder)
                .ThenInclude(a => a.DemandProviderProductionOrders)
                .ThenInclude(a => a.DemandRequester)
                .Where(a => a.ProductionOrder.DemandProviderProductionOrders.First().DemandRequester.DemandRequesterId == demand.Id 
                    || a.ProductionOrder.DemandProviderProductionOrders.First().DemandRequesterId == demand.Id)
                .ToList();
            
                
            foreach (var schedule in schedules)
            {
                //if forward was calculated take forward, else take from backward termination
                if (schedule.EndForward - schedule.StartForward > 0)
                {
                    schedule.End = schedule.EndForward;
                    schedule.Start = schedule.StartForward;
                }
                else
                {
                    schedule.End = schedule.EndBackward;
                    schedule.Start = schedule.StartBackward;
                }
                _context.ProductionOrderWorkSchedules.Update(schedule);
                _context.SaveChanges();
            }
        }

        /// <summary>
        /// Run requirements planning and backward/forward termination.
        /// </summary>
        /// <param name="demand"></param>
        /// <param name="task"></param>
        public void RunRequirementsAndTermination(IDemandToProvider demand, MrpTask task)
        {
            if (demand.State == State.Created)
            {
                ExecutePlanning(demand, task);
                if (demand.State != State.Finished)
                    demand.State = State.ProviderExist;
                _context.Update(demand);
                _context.SaveChanges();
                _messageHub.SendToAllClients("Requirements planning completed.");
            }
            
            if (task == MrpTask.All || task == MrpTask.Backward)
            {
                _scheduling.BackwardScheduling(demand);
                demand.State = State.BackwardScheduleExists;
                _context.Update(demand);
                _context.SaveChanges();
                _messageHub.SendToAllClients("Backward schedule exists.");

            }

            if ((task == MrpTask.All && CheckNeedForward(demand)) || task == MrpTask.Forward)
            {
                //schedules forward and then backward again with the finish of the forward algorithm
                _scheduling.ForwardScheduling(demand);
                demand.State = State.ForwardScheduleExists;
                _context.Update(demand);
                _context.SaveChanges();
                _scheduling.BackwardScheduling(demand);
                _messageHub.SendToAllClients("Forward schedule exists.");
            }
            _context.SaveChanges();
        }

        private bool CheckNeedForward(IDemandToProvider demand)
        {
            var pows = new List<ProductionOrderWorkSchedule>();
            return demand.GetType() == typeof(DemandStock) || _context.GetWorkSchedulesFromDemand(demand, ref pows).Any(a => a.StartBackward < _context.SimulationConfigurations.Last().Time);
        }

        private void ExecutePlanning(IDemandToProvider demand, MrpTask task)
        {
            //creates Provider for the needs
            var productionOrders = _demandForecast.NetRequirement(demand, task);
            if (demand.State != State.Finished)
                demand.State = State.ProviderExist;
            
            //If there was enough in stock this does not have to be produced
            if (!productionOrders.Any()) return;
            foreach (var productionOrder in productionOrders)
            {
                //if the ProductionOrder was just created, initialize concrete WorkSchedules for the ProductionOrders
                if (productionOrder.ProductionOrderWorkSchedule == null ||
                    !productionOrder.ProductionOrderWorkSchedule.Any())
                    _context.CreateProductionOrderWorkSchedules(productionOrder);
                
                var children = _context.ArticleBoms
                                    .Include(a => a.ArticleChild)
                                    .ThenInclude(a => a.ArticleBoms)
                                    .Where(a => a.ArticleParentId == productionOrder.ArticleId)
                                    .ToList();
                
                if (!children.Any()) return;
                foreach (var child in children)
                {
                    //create Requester
                    var dpob = _context.CreateDemandProductionOrderBom(child.ArticleChildId,productionOrder.Quantity * (int) child.Quantity);
                    //create Production-BOM
                    _context.TryCreateProductionOrderBoms(dpob, productionOrder);
                    
                    //call this method recursively for a depth-first search
                    ExecutePlanning(dpob, task);
                }
            }
        }

        public void UpdateDemandsAndOrders()
        {
            var requester = UpdateDemandStates();
            if (requester == null) return;
            var orderParts = (from singleRequester in requester
                              where singleRequester.GetType() == typeof(DemandOrderPart)
                              select ((DemandOrderPart) singleRequester).OrderPart).ToList();
            foreach (var orderPart in orderParts)
            {
                var finishedOrderPart =
                        _context.OrderParts.Include(a => a.Order)
                            .ThenInclude(b => b.OrderParts)
                            .ThenInclude(c => c.DemandOrderParts)
                            .ThenInclude(d => d.DemandProvider)
                            .Single(a => a.Id == orderPart.Id);
                if (finishedOrderPart.Order.OrderParts.Any(a => a.State != State.Finished))
                {
                    if (!TryFinishDemandsAndOrderParts(finishedOrderPart.Order))
                        continue;
                }
                
                FinishOrder(finishedOrderPart.Order);
                _context.SaveChanges();
            }
        }

        private bool TryFinishDemandsAndOrderParts(Order order)
        {
            var unfinishedOrderParts = order.OrderParts.Where(a => a.State != State.Finished).ToList();
            var unfinishedRequester = (from uop in unfinishedOrderParts
                                      from dop in uop.DemandOrderParts
                                      where dop.State != State.Finished
                                      select dop).ToList();
            /*if (unfinishedOrderParts.Any(a => a.DemandOrderParts.Any(b => b.State != State.Finished)))
            {
                if (unfinishedRequester.Any() && unfinishedRequester.Any(a => a.State != State.Finished))
                    return false;
            }*/
            foreach (var uop in unfinishedOrderParts)
            {
                foreach (var dop in uop.DemandOrderParts.Where(a => a.State != State.Finished))
                {
                    if (_context.TryUpdateStockProvider(dop))
                    {
                        dop.State = State.Finished;
                        _context.Update(dop);
                    }
                    else
                    {
                        _context.SaveChanges();
                        return false;
                    }
                }
                uop.State = State.Finished;
                _context.Update(uop);
            }
            _context.SaveChanges();
            return true;
        }


        private void FinishOrder(Order order)
        {
            order.State = State.Finished;
            _context.Update(order);
            _messageHub.SendToAllClients("Order with Id " + order.Id + " finished!");
            foreach (var singleOrderPart in order.OrderParts)
            {
                var stock = _context.Stocks.Single(a => a.ArticleForeignKey == singleOrderPart.ArticleId);
                stock.Current -= singleOrderPart.Quantity;
                _context.Update(stock);
            }
        }

        private List<IDemandToProvider> UpdateDemandStates()
        {
            var changedDemands = new List<IDemandToProvider>();
            changedDemands.AddRange(UpdateStateDemandProviderProductionOrders());
            changedDemands.AddRange(_context.UpdateStateDemandProviderPurchaseParts());

            return !changedDemands.Any() ? null : _context.UpdateStateDemandRequester(changedDemands);
        }

        private IEnumerable<IDemandToProvider> UpdateStateDemandProviderProductionOrders()
        {
            var changedDemands = new List<IDemandToProvider>();
            var provider = _context.Demands.OfType<DemandProviderProductionOrder>().Include(a => a.ProductionOrder).ThenInclude(b => b.ProductionOrderWorkSchedule).Where(a => a.State != State.Finished).ToList();
            foreach (var singleProvider in provider)
            {
                if (singleProvider.ProductionOrder.ProductionOrderWorkSchedule.Any(a => a.ProducingState != ProducingState.Finished)) continue;
                singleProvider.State = State.Finished;
                _context.Update(singleProvider);
                _context.SaveChanges();
                changedDemands.Add(singleProvider);
            }
            return changedDemands;
        }
    }

    public enum MrpTask
    {
        All,
        Forward,
        Backward,
        Capacity,
        GifflerThompson
    }


}