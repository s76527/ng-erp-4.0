﻿module FOperations

open System
open Akka.Actor
open Master40.DB.DataModel
open FProposals
open FStartConditions
open IKeys
open IJobs

    type public FOperation =
        { Key : Guid
          DueTime : int64 
          CreationTime : int64
          BackwardEnd : int64 
          BackwardStart : int64 
          End : int64 
          ForwardEnd : int64 
          ForwardStart : int64 
          Start : int64
          StartConditions : FStartCondition
          Priority : int64 -> double
          ProductionAgent : IActorRef
          ResourceAgent : IActorRef
          HubAgent : IActorRef
          Operation : M_Operation
          Proposals : System.Collections.Generic.List<FProposal> 
          } interface IKey with
                member this.Key  with get() = this.Key
                member this.CreationTime with get() = this.CreationTime
            interface IJob with
                member this.SetEstimatedEnd e = { this with End = e } :> IJob
                member this.BackwardEnd with get() = this.BackwardEnd
                member this.BackwardStart with get() = this.BackwardStart
                member this.DueTime with get() = this.DueTime
                member this.End with get() = this.End
                member this.ForwardEnd with get() = this.ForwardEnd
                member this.ForwardStart with get() = this.ForwardStart
                member this.Proposals with get() = this.Proposals
                member this.Start with get() = this.Start
                member this.StartConditions with get() = this.StartConditions
                member this.Priority time = this.Priority time 
                member this.ResourceAgent with get() = this.ResourceAgent
                member this.HubAgent with get() = this.HubAgent
                member this.Duration = (int64)this.Operation.Duration // Theoretisch muss hier die Slacktime noch rein also , +3*duration bzw aus dem operationElement
            interface IComparable with 
                member this.CompareTo fWorkItem = 
                    match fWorkItem with 
                    | :? FOperation as other -> compare other.Key this.Key
                    | _ -> invalidArg "Operation" "cannot compare value of different types" 
        member this.UpdatePoductionAgent p = { this with ProductionAgent = p }  
        member this.UpdateResourceAgent r = { this with ResourceAgent = r }
        member this.UpdateHubAgent hub = { this with HubAgent = hub }
        member this.SetForwardSchedule earliestStart = { this with ForwardStart = earliestStart;
                                                                   ForwardEnd = earliestStart + (int64)this.Operation.Duration; }