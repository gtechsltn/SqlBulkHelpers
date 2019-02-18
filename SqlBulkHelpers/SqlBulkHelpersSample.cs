﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Reflection;
using System.Diagnostics;
using SqlBulkHelpers;

namespace Debug.ConsoleApp
{
    public class SqlBulkHelpersSample
    {
        public static async Task Run()
        {
            using (var conn = await SqlBulkHelpersConnectionProvider.NewConnectionAsync())
            {
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    var tableName = "__BBERNARD_TEST";
                    var sqlBulkIdentityHelper = new SqlBulkIdentityHelper<TestElement>();

                    var timer = new Stopwatch();

                    //WARM UP THE CODE and initialize all CACHES!
                    timer.Start();
                    var testData = CreateTestData(1);

                    await sqlBulkIdentityHelper.BulkInsertOrUpdateAsync(testData, tableName, transaction);
                    await sqlBulkIdentityHelper.BulkInsertOrUpdateAsync(testData, tableName, transaction);

                    timer.Stop();
                    Console.WriteLine($"Warm Up ran in [{timer.ElapsedMilliseconds} ms]...");


                    //NOW RUN BENCHMARK LOOPS
                    int itemCounter = 0, batchCounter = 1, dataSize = 1000;
                    timer.Reset();
                    for (; batchCounter < 20; batchCounter++)
                    {
                        testData = SqlBulkHelpersSample.CreateTestData(dataSize);

                        timer.Start();
                        await sqlBulkIdentityHelper.BulkInsertAsync(testData, tableName, transaction);
                        timer.Stop();

                        itemCounter += testData.Count;
                    }

                    transaction.Commit();
                    Console.WriteLine($"[{batchCounter}] Bulk Uploads of [{dataSize}] items each executed in [{timer.ElapsedMilliseconds} ms] at ~[{timer.ElapsedMilliseconds / batchCounter} ms] each!");


                    var tableCount = 0;
                    using (var sqlCmd = conn.CreateCommand())
                    {
                        sqlCmd.CommandText = $"SELECT COUNT(*) FROM [{tableName}]";
                        tableCount = Convert.ToInt32(sqlCmd.ExecuteScalar());
                    }
                    Console.WriteLine($"[{tableCount}] Total Items in the Table Now!");
                    Console.ReadKey();
                }
            }
        }

        public static List<TestElement> CreateTestData(int dataSize)
        {

            var list = new List<TestElement>();
            for (var x = 1; x <= dataSize; x++)
            {
                list.Add(new TestElement()
                {
                    Id = default(int),
                    Key = $"TEST_CSHARP_{Guid.NewGuid()}_{x}",
                    Value = $"VALUE_{x}"
                });
            }

            return list;
        }

        public class TestElement : BaseIdentityIdModel
        {
            public String Key { get; set; }
            public String Value { get; set; }
        }
    }
}
