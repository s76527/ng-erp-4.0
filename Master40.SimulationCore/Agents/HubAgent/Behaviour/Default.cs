﻿using Akka.Actor;
using Master40.DB.Enums;
using Master40.SimulationCore.Agents.ProductionAgent;
using Master40.SimulationCore.Agents.ResourceAgent;
using Master40.SimulationCore.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using static FBreakDowns;
using static FAgentInformations;
using static FOperationResults;
using static FOperations;
using static FProposals;
using static IJobs;

namespace Master40.SimulationCore.Agents.HubAgent.Behaviour
{
    public class Default : SimulationCore.Types.Behaviour
    {
        internal Default(SimulationType simulationType = SimulationType.None)
                        : base(childMaker: null, obj: simulationType) { }


        internal List<FOperation> _operationList { get; set; } = new List<FOperation>();
        internal AgentDictionary _resourceAgents { get; set; } = new AgentDictionary();

        public override bool Action(object message)
        {
            switch (message)
            {
                case Hub.Instruction.EnqueueJob msg: EnqueueJob(fOperation: msg.GetObjectFromMessage as FOperation); break;
                case Hub.Instruction.ProposalFromResource msg: ProposalFromResource(fProposal: msg.GetObjectFromMessage); break;
                case Hub.Instruction.SetOperationArticleProvided msg: UpdateAndForwardArticleProvided(operationKey: msg.GetObjectFromMessage); break;
                case BasicInstruction.WithdrawRequiredArticles msg: WithdrawRequiredArticles(operationKey: msg.GetObjectFromMessage); break;
                case Hub.Instruction.FinishJob msg: FinishJob(operationResult: msg.GetObjectFromMessage as FOperationResult); break;
                case Hub.Instruction.AddResourceToHub msg: AddResourceToHub(hubInformation: msg.GetObjectFromMessage); break;
                case BasicInstruction.ResourceBrakeDown msg: ResourceBreakDown(breakDown: msg.GetObjectFromMessage); break;
                default: return false;
            }
            return true;
        }

        private void ResourceBreakDown(FBreakDown breakDown)
        {
            var brockenMachine = _resourceAgents.Single(predicate: x => breakDown.Resource == x.Value).Key;
            _resourceAgents.Remove(key: brockenMachine);
            Agent.Send(instruction: BasicInstruction.ResourceBrakeDown.Create(message: breakDown, target: brockenMachine, logThis: true), waitFor: 45);

            System.Diagnostics.Debug.WriteLine(message: "Break for " + breakDown.Resource, category: "Hub");
        }

        private void EnqueueJob(FOperation fOperation)
        {
            var localItem = _operationList.FirstOrDefault(x => x.Key == fOperation.Key);
            // If item is not Already in Queue Add item to Queue
            // // happens i.e. Machine calls to Requeue item.
            if (localItem == null)
            {
                localItem = fOperation;
                localItem.UpdateHubAgent(hub: Agent.Context.Self);
                _operationList.Add(item: localItem);

                Agent.DebugMessage(msg: "Got New Item to Enqueue: " + fOperation.Operation.Name + " | with start condition:" + fOperation.StartConditions.Satisfied + " with Id: " + fOperation.Key);
            }
            else
            {
                // reset Item.
                Agent.DebugMessage(msg: "Got Item to Requeue: " + fOperation.Operation.Name + " | with start condition:" + fOperation.StartConditions.Satisfied + " with Id: " + fOperation.Key);
                fOperation.Proposals.Clear();
                localItem = fOperation;
                //localItem = fOperation.UpdateHubAgent(hub: Agent.Context.Self);
                _operationList.Replace(val: localItem);
            }

            foreach (var actorRef in _resourceAgents)
            {
                Agent.Send(instruction: Resource.Instruction.RequestProposal.Create(message: localItem, target: actorRef.Key));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="proposal"></param>
        private void ProposalFromResource(FProposal fProposal)
        {
            // get related operation and add proposal.
            var fOperation = _operationList.Single(predicate: x => x.Key == fProposal.JobKey);
            fOperation.Proposals.RemoveAll(x => x.ResourceAgent.Equals(fProposal.ResourceAgent));
            // add New Proposal
            fOperation.Proposals.Add(item: fProposal);

            Agent.DebugMessage(msg: $"Proposal for Schedule: {fProposal.PossibleSchedule} Id: {fProposal.JobKey} from: {fProposal.ResourceAgent}!");


            // if all Machines Answered
            if (fOperation.Proposals.Count == _resourceAgents.Count)
            {

                // item Postponed by All Machines ? -> requeue after given amount of time.
                if (fOperation.Proposals.TrueForAll(match: x => x.Postponed.IsPostponed))
                {
                    // Call Hub Agent to Requeue
                    fOperation = fOperation.UpdateResourceAgent(r: ActorRefs.NoSender);
                    _operationList.Replace(val: fOperation);
                    Agent.Send(instruction: Hub.Instruction.EnqueueJob.Create(message: fOperation, target: Agent.Context.Self), waitFor: fProposal.Postponed.Offset);
                    return;
                }


                // acknowledge Machine -> therefore get Machine -> send acknowledgement
                var earliestPossibleStart = fOperation.Proposals.Where(predicate: y => y.Postponed.IsPostponed == false)
                                                               .Min(selector: p => p.PossibleSchedule);

                var acknowledgement = fOperation.Proposals.First(predicate: x => x.PossibleSchedule == earliestPossibleStart
                                                                        && x.Postponed.IsPostponed == false);

                fOperation = ((IJob)fOperation).UpdateEstimations(acknowledgement.PossibleSchedule, acknowledgement.ResourceAgent) as FOperation;

                Agent.DebugMessage(msg:$"Start AcknowledgeProposal for {fOperation.Operation.Name} {fOperation.Key} on resource {acknowledgement.ResourceAgent}");

                // set Proposal Start for Machine to Requeue if time slot is closed.
                _operationList.Replace(fOperation);
                Agent.Send(instruction: Resource.Instruction.AcknowledgeProposal.Create(message: fOperation, target: acknowledgement.ResourceAgent));
            }
        }

        private void UpdateAndForwardArticleProvided(Guid operationKey)
        {
            var temp = _operationList.Single(predicate: x => x.Key == operationKey);
            temp.StartConditions.ArticlesProvided = true;
            if (temp.ResourceAgent.IsNobody())
            {
                return;
            }

            Agent.DebugMessage(msg: $"UpdateAndForwardArticleProvided {temp.Operation.Name} {temp.Key} to resource {temp.ResourceAgent}");

            Agent.Send(instruction: Resource.Instruction.UpdateArticleProvided.Create(message: temp.Key, target: temp.ResourceAgent));
        }

        /// <summary>
        /// Source: ResourceAgent put operation onto processingQueue and will work on it soon
        /// Target: Method should forward message to the associated production agent
        /// </summary>
        /// <param name="key"></param>
        public void WithdrawRequiredArticles(Guid operationKey)
        {
            var operation = _operationList.Single(predicate: x => x.Key == operationKey);
            Agent.Send(instruction: BasicInstruction.WithdrawRequiredArticles
                                                    .Create(message: operation.Key
                                                           , target: operation.ProductionAgent));
            // TODO Necessary? 
            //_operationList.Replace(val: operation);  ??
        }

        public void FinishJob(FOperationResult operationResult)
        {
            var item = _operationList.Find(match: x => x.Key == operationResult.Key);

            Agent.DebugMessage(msg: "Machine called Item " + item.Operation.Name + " with Id: " + operationResult.Key + " finished.");
            Agent.Send(instruction: Production.Instruction.FinishWorkItem.Create(message: operationResult, target: item.ProductionAgent));
            _operationList.Remove(item: item);
        }


        private void AddResourceToHub(FAgentInformation hubInformation)
        {
            // Add Machine to Pool
            _resourceAgents.Add(key: hubInformation.Ref, value: hubInformation.RequiredFor);
            // Added Machine Agent To Machine Pool
            Agent.DebugMessage(msg: "Added Machine Agent " + hubInformation.Ref.Path.Name + " to Machine Pool: " + hubInformation.RequiredFor);
        }
    }
}