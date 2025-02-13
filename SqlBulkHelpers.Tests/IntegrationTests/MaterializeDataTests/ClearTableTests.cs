﻿using System;
using SqlBulkHelpers.Tests;
using SqlBulkHelpers.MaterializedData;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace SqlBulkHelpers.IntegrationTests
{
    [TestClass]
    public class MaterializeDataClearTableTests : BaseTest
    {
        /// <summary>
        /// NOTE: ALL TESTS Assume that the tables started off in an empty state so we clear them.... Ironically we are also testing this Feature
        ///     of clearing, but any failure of this or the tests points to an issue so we use the feature to prep for testing the feature
        ///     and if everything passes then we are good go go :-)
        /// </summary>
        /// <returns></returns>
        [TestInitialize]
        public async Task SetupEachTestAsync()
        {
            var sqlConnectionProvider = SqlConnectionHelper.GetConnectionProvider();

            await using (var sqlConn = await sqlConnectionProvider.NewConnectionAsync().ConfigureAwait(false))
            await using (var sqlTrans = (SqlTransaction)await sqlConn.BeginTransactionAsync().ConfigureAwait(false))
            {
                await sqlTrans.ClearTablesAsync(new string[]
                {
                    TestHelpers.TestTableNameFullyQualified,
                    TestHelpers.TestChildTableNameFullyQualified
                }, forceOverrideOfConstraints: true).ConfigureAwait(false);

                await sqlTrans.CommitAsync().ConfigureAwait(false);
            }
        }

        [TestMethod]
        public async Task TestClearTableAsync()
        {
            var sqlConnectionProvider = SqlConnectionHelper.GetConnectionProvider();

            await using (var sqlConn = await sqlConnectionProvider.NewConnectionAsync().ConfigureAwait(false))
            await using (var sqlTrans = (SqlTransaction)await sqlConn.BeginTransactionAsync().ConfigureAwait(false))
            {
                //NOTE: the TestChildTableName does not have any FKey constraints so it can be safely truncated!
                var tableNameTerm = TestHelpers.TestChildTableNameFullyQualified.ParseAsTableNameTerm();

                //Ensure at least some data exists...
                var parentTestData = TestHelpers.CreateTestData(10, "TEST_TRUNCATION_DotNet6");
                var parentResults = await sqlTrans.BulkInsertAsync(parentTestData, TestHelpers.TestTableName).ConfigureAwait(false);
                
                var childTestData = TestHelpers.CreateChildTestData(parentResults.ToList());
                await sqlTrans.BulkInsertAsync(childTestData, tableNameTerm);

                //Get our count BEFORE and Validate we have some data...
                var initialTableCount = await sqlConn.CountAllAsync(tableNameTerm, transaction: sqlTrans).ConfigureAwait(false);
                Assert.IsTrue(initialTableCount > 0);

                //var initialTableCount = await sqlConn.CountAllAsync(tableNameTerm, transaction: sqlTrans).ConfigureAwait(false);
                var resultTable = await sqlTrans.ClearTableAsync(tableNameTerm);

                //ASSERT Results are Valid...
                Assert.IsNotNull(resultTable);

                //Get our count AFTER and assert they are valid...
                var truncatedTableCount = await sqlConn.CountAllAsync(tableName: resultTable, transaction: sqlTrans).ConfigureAwait(false);
                Assert.AreEqual(0, truncatedTableCount);

                await sqlTrans.RollbackAsync().ConfigureAwait(false);
            }
        }

        [TestMethod]
        public async Task TestClearTableFailsConstraintsAsync()
        {
            var sqlConnectionProvider = SqlConnectionHelper.GetConnectionProvider();

            await using (var sqlConn = await sqlConnectionProvider.NewConnectionAsync().ConfigureAwait(false))
            await using (var sqlTrans = (SqlTransaction)await sqlConn.BeginTransactionAsync().ConfigureAwait(false))
            {
                var targetTableNameTerm = TableNameTerm.From(TestHelpers.TestTableNameFullyQualified);
                
                //FIRST Validate that the Truncation with Constrains fails as expected (e.g. has FKey constraints)...
                Exception? failedException = null;
                try
                {
                    //Second attempt the clone again but this time expecting it to now already exist and fail out!
                    await sqlTrans.ClearTableAsync(targetTableNameTerm);
                }
                catch (Exception exc)
                {
                    failedException = exc;
                }

                Assert.IsNotNull(failedException);
            }
        }

        [TestMethod]
        public async Task TestClearTableForceWithConstraintsAsync()
        {
            var sqlConnectionProvider = SqlConnectionHelper.GetConnectionProvider();

            await using (var sqlConn = await sqlConnectionProvider.NewConnectionAsync().ConfigureAwait(false))
            await using (var sqlTrans = (SqlTransaction)await sqlConn.BeginTransactionAsync().ConfigureAwait(false))
            {
                var targetTableNameTerm = TableNameTerm.From(TestHelpers.TestTableNameFullyQualified);

                //Ensure at least some data exists...
                var existingCount = await sqlConn.CountAllAsync(targetTableNameTerm, transaction: sqlTrans);
                if (existingCount <= 0)
                    await sqlTrans.BulkInsertAsync(TestHelpers.CreateTestData(10, "TEST_TRUNCATION_DotNet6"), targetTableNameTerm);

                //Get our count BEFORE and Validate we have some data...
                var initialTableCount = await sqlConn.CountAllAsync(targetTableNameTerm, transaction: sqlTrans).ConfigureAwait(false);
                Assert.IsTrue(initialTableCount > 0);

                //NOW we can now test Truncation with support for overriding the constraints...
                var clearedTables = await sqlTrans.ClearTablesAsync(new string[]
                {
                    targetTableNameTerm,
                    TestHelpers.TestChildTableNameFullyQualified
                }, forceOverrideOfConstraints: true).ConfigureAwait(false);

                //ASSERT Results are Valid...
                Assert.IsNotNull(clearedTables);

                //Get our count AFTER and assert they are valid...
                foreach (var resultTable in clearedTables)
                {
                    var truncatedTableCount = await sqlConn.CountAllAsync(tableName: resultTable, transaction: sqlTrans).ConfigureAwait(false);
                    Assert.AreEqual(0, truncatedTableCount);
                }

                //await sqlTrans.RollbackAsync().ConfigureAwait(false);
                await sqlTrans.CommitAsync().ConfigureAwait(false);
            }
        }

        [TestMethod]
        public async Task TestClearMultipleTablesAsync()
        {
            var sqlConnectionProvider = SqlConnectionHelper.GetConnectionProvider();

            //Update our Configuration to provide a Connection Factory which Enables Concurrent Connection Processing for Performance...
            SqlBulkHelpersConfig.ConfigureDefaults(config => config.EnableConcurrentSqlConnectionProcessing(sqlConnectionProvider));

            await using (var sqlConn = await sqlConnectionProvider.NewConnectionAsync().ConfigureAwait(false))
            await using (var sqlTrans = (SqlTransaction)await sqlConn.BeginTransactionAsync().ConfigureAwait(false))
            {
                var parentTableNameTerm = TestHelpers.TestTableNameFullyQualified.ParseAsTableNameTerm();
                var childTableNameTerm = TestHelpers.TestChildTableNameFullyQualified.ParseAsTableNameTerm();

                //Ensure at least some data exists...
                //Insert Parent Data...
                var parentResults = (await sqlTrans.BulkInsertAsync(
                    TestHelpers.CreateTestData(10, "TEST_TRUNCATION_DotNet6"),
                    parentTableNameTerm
                )).ToList();
                //Insert Child Related Data...
                await sqlTrans.BulkInsertAsync(TestHelpers.CreateChildTestData(parentResults));

                //Get our count BEFORE and Validate we have some data...
                var initialParentCount = await sqlConn.CountAllAsync(parentTableNameTerm, transaction: sqlTrans).ConfigureAwait(false);
                Assert.IsTrue(initialParentCount > 0);
                var initialChildCount = await sqlConn.CountAllAsync(childTableNameTerm, transaction: sqlTrans).ConfigureAwait(false);
                Assert.IsTrue(initialChildCount > 0);

                //NOW we can now test Truncation with support for overriding the constraints...
                var tableResults = await sqlTrans.ClearTablesAsync(new string[] 
                {
                    childTableNameTerm, 
                    parentTableNameTerm
                }, forceOverrideOfConstraints: true).ConfigureAwait(false);

                Assert.IsNotNull(tableResults);
                Assert.AreEqual(2, tableResults.Length);

                foreach (var resultTable in tableResults)
                {
                    //Get our count AFTER and assert they are valid...
                    var truncatedTableCount = await sqlConn.CountAllAsync(tableName: resultTable, transaction: sqlTrans).ConfigureAwait(false);
                    Assert.AreEqual(0, truncatedTableCount);
                }

                //await sqlTrans.RollbackAsync().ConfigureAwait(false);
                await sqlTrans.CommitAsync().ConfigureAwait(false);
            }
        }

        //[TestMethod]
        //public async Task TestClearMAESTROMultipleTablesAsync()
        //{
        //    var sqlConnectionProvider = SqlConnectionHelper.GetConnectionProvider();

        //    await using (var sqlConn = await sqlConnectionProvider.NewConnectionAsync().ConfigureAwait(false))
        //    await using (var sqlTrans = (SqlTransaction)await sqlConn.BeginTransactionAsync().ConfigureAwait(false))
        //    {
        //        //NOW we can now test Truncation with support for overriding the constraints...
        //        var tableResults = await sqlTrans.ClearTablesAsync(new[]
        //        {
        //            "AdplanFacebookCampaign",
        //            "FacebookCampaign",
        //            "FacebookCampaignCreative"
        //        }, forceOverrideOfConstraints: true).ConfigureAwait(false);

        //        foreach (var resultTable in tableResults)
        //        {
        //            //Get our count AFTER and assert they are valid...
        //            var truncatedTableCount = await sqlConn.CountAllAsync(tableName: resultTable, transaction: sqlTrans).ConfigureAwait(false);
        //            Assert.AreEqual(0, truncatedTableCount);
        //        }

        //        //await sqlTrans.RollbackAsync().ConfigureAwait(false);
        //        await sqlTrans.CommitAsync().ConfigureAwait(false);
        //    }
        //}
    }
}
