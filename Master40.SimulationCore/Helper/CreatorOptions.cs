﻿using Akka.Actor;
using Master40.SimulationCore.Agents;
using Master40.SimulationCore.Helper;
using System;
using System.Collections.Generic;
using System.Text;

namespace Master40.SimulationCore.Helper
{
    public static class CreatorOptions
    {
        private static int contractCount = 0;
        private static int productionCount = 0;
        private static int dispoCount = 0;
        public static Func<IUntypedActorContext, AgentSetup, IActorRef> ContractCreator = (ctx, setup) =>
        {
            return ctx.ActorOf(Contract.Props(setup.ActorPaths, setup.Time, setup.Debug), "ContractAgent(" + contractCount++ + ")");
        };

        public static Func<IUntypedActorContext, AgentSetup, IActorRef> DispoCreator = (ctx, setup) =>
        {
            return ctx.ActorOf(Dispo.Props(setup.ActorPaths, setup.Time, setup.Debug, setup.Principal), "DispoAgent(" + productionCount++ + ")");
        };


        public static Func<IUntypedActorContext, AgentSetup, IActorRef> ProductionCreator = (ctx, setup) =>
        {
            return ctx.ActorOf(Production.Props(setup.ActorPaths, setup.Time, setup.Debug, setup.Principal), "ProductionAgent(" + dispoCount++ + ")");
        };
    }
}
