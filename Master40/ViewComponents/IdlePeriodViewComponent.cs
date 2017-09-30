﻿using ChartJSCore.Models;
using Master40.DB.Data.Context;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Master40.DB.Enums;
using ChartJSCore.Models.Bar;
using Master40.Extensions;

namespace Master40.ViewComponents
{
    public class IdlePeriodViewComponent : ViewComponent
    {
        private readonly ProductionDomainContext _context;

        public IdlePeriodViewComponent(ProductionDomainContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync(List<string> paramsList)
        {
            SimulationType simType = (paramsList[1].Equals("Decentral")) ? SimulationType.Decentral : SimulationType.Central;
            var kpis = _context.Kpis.Where(x => x.KpiType == KpiType.LayTime
                                               && x.SimulationConfigurationId == Convert.ToInt32(paramsList[0])
                                               && x.SimulationType == simType
                                               && x.IsKpi == true);


            var generateChartTask = Task.Run(() =>
            {
            if (!_context.SimulationWorkschedules.Any())
            {
                return null;
            }

            Chart chart = new Chart();
            var cc = new ChartColor();
            // charttype
            chart.Type = "horizontalBar";

            var data = new Data { Datasets = new List<Dataset>(),
                                  Labels = kpis.Select(x => x.Name).ToList() };

            var min = new BarDataset {
                Data = new List<double>(),
                BackgroundColor = new List<string>() // { cc.Color[8], cc.Color[4], cc.Color[1] } 
            };
            var avg = new BarDataset {
                Data = new List<double>(),
                BackgroundColor = new List<string>() // { cc.Color[8], , cc.Color[1] }
            };
            var max = new BarDataset {
                Data = new List<double>(),
                BackgroundColor = new List<string>() // { cc.Color[8], cc.Color[4], cc.Color[1] }
            };

            foreach (var kpi in kpis)
            {
                min.Data.Add(kpi.ValueMin);
                avg.Data.Add(kpi.Value);
                max.Data.Add(kpi.ValueMax);
                min.BackgroundColor.Add(ChartColor.Transparent);
                avg.BackgroundColor.Add(cc.Color[4]);
                max.BackgroundColor.Add(cc.Color[1]);
            }

            data.Datasets.Add(min);
            data.Datasets.Add(avg);
            data.Datasets.Add(max);

            var xAxis = new List<Scale>() { new BarScale { Stacked = true } };
            var yAxis = new List<Scale>() { new BarScale { Stacked = true } }; // Ticks = new Tick { BeginAtZero = true, Min = 0, Max = 100 }
            //var yAxis = new List<Scale>() { new BarScale{ Ticks = new CategoryTick { Min = "0", Max  = (yMaxScale * 1.1).ToString() } } };
            chart.Options = new Options() { Scales = new Scales { XAxes = xAxis, YAxes = yAxis }, MaintainAspectRatio = false, Responsive = true, Legend = new Legend { Display = false } };
            chart.Data = data;
            return chart;
            });

            // create JS to Render Chart.
            ViewData["chart"] = await generateChartTask;
            ViewData["Type"] = paramsList[1];
            ViewData["Data"] = kpis.ToList();
            return View($"IdlePeriod");
        }
    }
}
