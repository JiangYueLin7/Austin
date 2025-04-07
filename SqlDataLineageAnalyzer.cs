using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Austin
{
    public class SqlDataLineageAnalyzer : TSqlFragmentVisitor
    {
        private readonly Dictionary<string, TableInfo> _tableRegistry = new();
        private readonly Stack<QueryContext> _queryStack = new();

        public class TableInfo
        {
            public string Name { get; set; }
            public TableType Type { get; set; }
            public List<ColumnInfo> Columns { get; set; }
            public List<LineageEdge> Dependencies { get; set; }
        }

        public enum TableType { Physical, View, Temp }

        public class ColumnInfo
        {
            public string OriginalName { get; set; }
            public string Alias { get; set; }
            public TableInfo ParentTable { get; set; }
            public List<LineageEdge> ColumnDependencies { get; set; }
            public string DataType { get; set; }
        }
        // 辅助类定义
        public class TempTableInfo
        {
            public string Name { get; set; }
            public TableType Type { get; set; }
            public TempTableSourceType SourceType { get; set; }
            public List<ColumnInfo> Columns { get; set; }
        }

        public enum TempTableSourceType { CreateTable, SelectInto }

        public class LineageEdge
        {
            public string FromTable { get; set; }
            public string ToTable { get; set; }
            public string ColumnName { get; set; }
            public string Operation { get; set; }
        }

        public class QueryContext
        {
            public TableInfo CurrentTable { get; set; }
            public List<ColumnInfo> OutputColumns { get; set; }
            public Dictionary<string, TableInfo> JoinedTables { get; set; }
        }
    }
}
