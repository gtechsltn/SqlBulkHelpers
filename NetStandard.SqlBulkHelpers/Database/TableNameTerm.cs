﻿using System;
using static System.Net.Mime.MediaTypeNames;

namespace SqlBulkHelpers
{
    public readonly struct TableNameTerm
    {
        public const string DefaultSchemaName = "dbo";
        public const char TermSeparator = '.';

        public TableNameTerm(string schemaName, string tableName)
        {
            SchemaName = schemaName.AssertArgumentIsNotNullOrWhiteSpace(nameof(schemaName)).TrimTableNameTerm();
            TableName = tableName.AssertArgumentIsNotNullOrWhiteSpace(nameof(tableName)).TrimTableNameTerm();
            //NOTE: We don't use QualifySqlTerm() here to prevent unnecessary additional trimming (that is done above).
            FullyQualifiedTableName = $"[{SchemaName}].[{TableName}]";
        }

        public string SchemaName { get; }
        public string TableName { get; }
        public string FullyQualifiedTableName { get; }

        public override string ToString() => FullyQualifiedTableName;
        public TableNameTerm SwitchSchema(string newSchema) => new TableNameTerm(newSchema, TableName);
        public static implicit operator string(TableNameTerm t) => t.ToString();

        public static TableNameTerm From(string schemaName, string tableName)
            => new TableNameTerm(schemaName, tableName);

        public static TableNameTerm From(string tableNameOverride)
            => From<ISkipMappingLookup>(tableNameOverride);

        public static TableNameTerm From<T>(string tableNameOverride = null)
        {
            TableNameTerm tableNameTerm;
            if (tableNameOverride != null)
            {
                tableNameTerm = tableNameOverride.ParseAsTableNameTerm();
            }
            else
            {
                var processingDef = SqlBulkHelpersProcessingDefinition.GetProcessingDefinition<T>();
                tableNameTerm = processingDef.MappedDbTableName.ParseAsTableNameTerm();
            }
            return tableNameTerm;
        }
    }
}
