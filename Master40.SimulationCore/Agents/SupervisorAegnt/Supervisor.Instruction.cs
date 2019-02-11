﻿using Akka.Actor;
using AkkaSim.Definitions;
using Master40.DB.Models;
using Master40.SimulationImmutables;

namespace Master40.SimulationCore.Agents
{
    public partial class Supervisor
    {
        public class Instruction
        {
            /*
                CreateContractAgent,
                RequestArticleBom,
                OrderProvided,
            */
            public class CreateContractAgent : SimulationMessage
            {
                public static CreateContractAgent Create(OrderPart message, IActorRef target)
                {
                    return new CreateContractAgent(message, target);
                }
                private CreateContractAgent(OrderPart message, IActorRef target) : base(message, target)
                {
                }
                public OrderPart GetObjectFromMessage { get => Message as OrderPart; }

            }
            public class RequestArticleBom : SimulationMessage
            {
                public static RequestArticleBom Create(FRequestItem message, IActorRef target)
                {
                    return new RequestArticleBom(message, target);
                }
                private RequestArticleBom(object message, IActorRef target) : base(message, target)
                {
                }
                public FRequestItem GetObjectFromMessage { get => Message as FRequestItem; }
            }
            public class OrderProvided : SimulationMessage
            {
                public static OrderProvided Create(object message, IActorRef target)
                {
                    return new OrderProvided(message, target);
                }
                private OrderProvided(object message, IActorRef target) : base(message, target)
                {
                }
            }

            public class Inizialized : SimulationMessage
            {
                public Inizialized() : base (true, ActorRefs.Nobody) { }
            }

            public class EndSimulation : SimulationMessage
            {
                public static EndSimulation Create(object message, IActorRef target)
                {
                    return new EndSimulation(message, target);
                }
                private EndSimulation(object message, IActorRef target) : base(message, target)
                {
                }
            }


            public class PopOrder : SimulationMessage
            {
                public static PopOrder Create(string message, IActorRef target)
                {
                    return new PopOrder(message, target);
                }
                private PopOrder(object message, IActorRef target) : base(message, target)
                {
                }
            }
        }
    }
}