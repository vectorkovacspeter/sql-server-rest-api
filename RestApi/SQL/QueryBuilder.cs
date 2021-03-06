﻿// Copyright (c) Jovan Popovic. All Rights Reserved.
// Licensed under the BSD License. See LICENSE.txt in the project root for license information.

using System.Collections;
using System.Data.SqlClient;
using System.Text;

namespace MsSql.RestApi
{
    public static class QueryBuilder
    {
        
        public static SqlCommand Build(QuerySpec spec, TableSpec table)
        {
            SqlCommand res = new SqlCommand();
            StringBuilder sql = new StringBuilder();

            BuildSelectFromClause(spec, table, sql);
            // We should build WHERE clause even if we need just a count.
            // Client may need count of filtered rows.
            BuildWherePredicate(spec, sql, table);
            BuildGroupByClause(spec, res, sql, table);
            if (!spec.count)
            {
                // Don't need ORDER BY for count
                BuildOrderByClause(spec, sql);
                // Don't need pagination for count
                BuildOffsetFetchClause(spec, sql);
            }
            
            res.CommandText = sql.ToString();
            AddParametersToSqlCommand(spec, res);
            res.CommandTimeout = 360;
            return res;
        }

        private static void BuildGroupByClause(QuerySpec spec, SqlCommand res, StringBuilder sql, TableSpec table)
        {
            if (spec.groupBy != null)
            {
                sql.Append(" GROUP BY ").Append(spec.groupBy);
            }
        }

        private static void BuildSelectFromClause(QuerySpec spec, TableSpec table, StringBuilder sql)
        {
            sql.Append("SELECT ");

            if (spec.count) {
                sql.Append("CAST(count(*) as nvarchar(50)) ");
            }
            else
            {
                if (spec.top >= 0 && spec.skip <= 0)
                    sql.Append("TOP ").Append(spec.top).Append(" ");

                sql.Append(spec.select ?? table.columnList ?? "*");
            }
            BuildExpand(spec, table, sql);
            sql.Append(" FROM ");
            sql.Append(table.FullName);

            if(!string.IsNullOrWhiteSpace(spec.systemTimeAsOf))
                sql.Append(" FOR SYSTEM_TIME AS OF '").Append(spec.systemTimeAsOf).Append("'");
        }

        private static void BuildExpand(QuerySpec spec, TableSpec table, StringBuilder sql)
        {
            if (spec.expand == null || spec.expand.Count == 0)
                return;

            foreach (var expansion in spec.expand)
            {
                sql.Append(',');
                sql.Append(expansion.Key).Append("=(");
                BuildSelectFromClause(expansion.Value, table.Relations[expansion.Key], sql);
                //sql.Append(" WHERE ").Append(table.Relations[expansion.Key].primaryKey);


                BuildWherePredicate(expansion.Value, sql, table.Relations[expansion.Key]);
                // should I suport group by in expand? 
                // BuildGroupByClause(expansion.Value, res, sql, table.Relations[expansion.Key]);
                if (!spec.count)
                {
                    // Don't need ORDER BY for count
                    BuildOrderByClause(expansion.Value, sql);
                    // Don't need pagination for count
                    BuildOffsetFetchClause(expansion.Value, sql);
                }
                sql.Append(" FOR JSON PATH)");
            }
        }

        /// <summary>
        /// co11 LIKE @kwd OR col2 LIKE @kwd ...
        /// OR
        /// (col1 LIKE @srch1 AND col2 LIKE srch2 ...)
        /// </summary>
        /// <param name="spec"></param>
        /// <param name="res"></param>
        private static void BuildWherePredicate(QuerySpec spec, StringBuilder sql, TableSpec table)
        {
            bool isWhereClauseAdded = false;

            

            if (!string.IsNullOrEmpty(spec.predicate))
            {
                // This is T-SQL predicate that is provided via Url (e.g. using OData $filter clause)
                sql.Append(" WHERE (").Append(spec.predicate).Append(")");
                isWhereClauseAdded = true;
            }

            if (!string.IsNullOrEmpty(spec.keyword))
            {
                if (!isWhereClauseAdded)
                {
                    sql.Append(" WHERE (");
                    isWhereClauseAdded = true;
                }
                else
                {
                    sql.Append(" OR (");
                }

                bool isFirstColumn = true;
                foreach (var column in table.columns)
                {
                    if (!isFirstColumn)
                    {
                        sql.Append(" OR ");
                    }
                    sql.Append("(").Append(column.Name).Append(" like @kwd)");
                    isFirstColumn = false;
                }
                sql.Append(" ) "); // Add closing ) for WHERE ( or OR ( that is added in this block
            }

            // Add filter predicates for individual columns.
            if (spec.columnFilter != null && spec.columnFilter.Count > 0)
            {
                bool isFirstColumn = true, isWhereClauseAddedInColumnFiler = false;
                foreach (DictionaryEntry entry in spec.columnFilter)
                {
                    if (!string.IsNullOrEmpty(entry.Value.ToString()))
                    {

                        if (isFirstColumn)
                        {
                            if (!isWhereClauseAdded)
                            {
                                sql.Append(" WHERE (");
                            }
                            else
                            {
                                sql.Append(" OR (");
                            }
                            isWhereClauseAddedInColumnFiler = true;
                        }
                        else
                        {
                            sql.Append(" AND ");
                        }

                        sql.Append("(").Append(entry.Key.ToString()).Append(" LIKE @").Append(entry.Key.ToString()).Append(")");
                        isFirstColumn = false;
                    }
                }
                if (isWhereClauseAddedInColumnFiler)
                {
                    sql.Append(")");
                }
            }

        }

        private static void AddParametersToSqlCommand(QuerySpec spec, SqlCommand res)
        {
            // If there is a global search by keyword in JQuery Datatable or $search param in OData add this parameter.
            if (!string.IsNullOrEmpty(spec.keyword))
            {
                var p = new SqlParameter("kwd", System.Data.SqlDbType.NVarChar, 4000);
                p.Value = "%" + spec.keyword + "%";
                res.Parameters.Add(p);
            }

            // If there are some literals that are transformed to parameters add them in command.
            if (spec.parameters != null && spec.parameters.Count > 0)
            {
                foreach (var parameter in spec.parameters)
                {
                    res.Parameters.Add(parameter);
                }
            }

            // If there are filters per columns add them as parameters
            if (spec.columnFilter != null && spec.columnFilter.Count > 0)
            {
                foreach (DictionaryEntry entry in spec.columnFilter)
                {
                    if (string.IsNullOrEmpty(entry.Value.ToString()))
                        continue;
                    var p = new SqlParameter(entry.Key.ToString(), System.Data.SqlDbType.NVarChar, 4000);
                    p.Value = "%" + entry.Value + "%";
                    res.Parameters.Add(p);
                }
            }
        }

        private static void BuildOrderByClause(QuerySpec spec, StringBuilder sql)
        {
            if (spec.order != null && spec.order.Count > 0)
            {
                bool first = true;
                foreach (DictionaryEntry entry in spec.order)
                {
                    if (first)
                    {
                        sql.Append(" ORDER BY ");
                        first = false;
                    }
                    else
                        sql.Append(" , ");

                    sql.Append(entry.Key.ToString()).Append(" ").Append(entry.Value.ToString());
                }
            }
        }
        
        private static void BuildOffsetFetchClause(QuerySpec spec, StringBuilder sql)
        {
            if (spec.top >= 0 && spec.skip <= 0)
                return; // This case is covered with TOP

            // At this point we know that spec.skip is != null

            if (spec.order == null || spec.order.Keys.Count == 0)
                sql.Append(" ORDER BY 1 "); // Add mandatory order by clause if it is not already there.

            if (spec.skip >= 0)
                sql.AppendFormat(" OFFSET {0} ROWS ", spec.skip);

            if (spec.top >= 0)
                sql.AppendFormat(" FETCH NEXT {0} ROWS ONLY ", spec.top);
            
        }
    }
}
