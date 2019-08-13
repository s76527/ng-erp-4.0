﻿using Akka.Actor;
using AkkaSim.Definitions;
using Master40.DB.DataModel;
using static FArticles;
using static FStockReservations;

namespace Master40.SimulationCore.Agents.DispoAgent
{
    public partial class Dispo
    {
        public class Instruction
        {
            public class RequestArticle : SimulationMessage
            {
                public static RequestArticle Create(FArticle message, IActorRef target)
                {
                    return new RequestArticle(message, target);
                }
                private RequestArticle(object message, IActorRef target) : base(message, target)
                {
                }
                public FArticle GetObjectFromMessage { get => Message as FArticle; }
            }

            public class ResponseFromStock : SimulationMessage
            {
                public static ResponseFromStock Create(FStockReservation message, IActorRef target)
                {
                    return new ResponseFromStock(message, target);
                }
                private ResponseFromStock(object message, IActorRef target) : base(message, target)
                {

                }
                public FStockReservation GetObjectFromMessage { get => this.Message as FStockReservation; }
            }
            public class RequestProvided : SimulationMessage
            {
                public static RequestProvided Create(FArticle message, IActorRef target)
                {
                    return new RequestProvided(message, target);
                }
                private RequestProvided(object message, IActorRef target) : base(message, target)
                {

                }
                public FArticle GetObjectFromMessage { get => Message as FArticle; }
            }

            public class ResponseFromSystemForBom : SimulationMessage
            {
                public static ResponseFromSystemForBom Create(M_Article message, IActorRef target)
                {
                    return new ResponseFromSystemForBom(message, target);
                }
                private ResponseFromSystemForBom(object message, IActorRef target) : base(message, target)
                {
                }
                public M_Article GetObjectFromMessage { get => Message as M_Article; }
            }
            public class WithdrawMaterialsFromStock : SimulationMessage
            {
                public static WithdrawMaterialsFromStock Create(object message, IActorRef target)
                {
                    return new WithdrawMaterialsFromStock(message, target);
                }
                private WithdrawMaterialsFromStock(object message, IActorRef target) : base(message, target)
                {

                }
            }
        }
    }
}