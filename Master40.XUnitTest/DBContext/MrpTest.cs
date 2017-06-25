﻿using System.Threading.Tasks;
using Master40.BusinessLogic.MRP;
using Master40.DB.Data.Context;
using Master40.DB.Data.Initializer;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Master40.XUnitTest.DBContext
{
    public class MrpTest
    {
        public async Task CreateAndProcessOrderDemandAll(ProcessMrp processMrp)
        {
            await processMrp.CreateAndProcessOrderDemand(MrpTask.All);
        }
    }
}