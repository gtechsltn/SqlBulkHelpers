﻿using System;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SqlBulkHelpers
{
    public static class SqlBulkHelpersCustomExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
        {
            TValue value;
            return dictionary.TryGetValue(key, out value) ? value : default(TValue);
        }

        public static String ToCSV(this IEnumerable<String> enumerableList)
        {
            return String.Join(", ", enumerableList);
        }
    }

    public static class SystemDataSqlClientCustomExtensions
    {
        public static IEnumerable<T> Enumerate<T>(this T reader) where T : IDataReader
        {
            while (reader.Read()) yield return reader;
        }

        public static T ExecuteForJson<T>(this SqlCommand sqlCmd) where T : class
        {
            //Quickly Read the FIRST record fully from Sql Server Reader response.
            using (var sqlReader = sqlCmd.ExecuteReader())
            {
                //Short circuit if no data is returned.
                if (sqlReader.HasRows)
                {
                    var jsonStringBuilder = new StringBuilder();
                    while (sqlReader.Read())
                    {
                        jsonStringBuilder.Append(sqlReader.GetString(0));
                    }

                    var json = jsonStringBuilder.ToString();
                    var result = JsonConvert.DeserializeObject<T>(json);
                    return result;
                }
            }

            return null;
        }


        public static async Task<T> ExecuteForJsonAsync<T>(this SqlCommand sqlCmd) where T : class
        {
            //Quickly Read the FIRST record fully from Sql Server Reader response.
            using (var sqlReader = await sqlCmd.ExecuteReaderAsync())
            {
                //Short circuit if no data is returned.
                if (sqlReader.HasRows)
                {
                    var jsonStringBuilder = new StringBuilder();
                    while (await sqlReader.ReadAsync())
                    {
                        //So far all calls to SqlDataReader have been asynchronous, but since the data reader is in 
                        //non -sequential mode and ReadAsync was used, the column data should be read synchronously.
                        jsonStringBuilder.Append(sqlReader.GetString(0));
                    }

                    var json = jsonStringBuilder.ToString();
                    var result = JsonConvert.DeserializeObject<T>(json);
                    return result;
                }
            }

            return null;
        }

    }
}
