﻿namespace Master40.DB.Nominal
{
    public enum State
    {
        Created,
        Injected,
        ProviderExist,
        BackwardScheduleExists,
        ForwardScheduleExists,
        ExistsInCapacityPlan,
        Producing,
        Finished,
        InProgress
    }

    public enum JobState
    {
        Revoked,
        RevokeStarted,
        Created,
        InQueue,
        WillBeReady,
        SetupReady,
        SetupInProcess,
        SetupFinished,
        Ready,
        InProcess,
        Finish,
    }

    public enum ProducingState
    {
        Created,
        Waiting,
        Producing,
        Finished
    }
}
