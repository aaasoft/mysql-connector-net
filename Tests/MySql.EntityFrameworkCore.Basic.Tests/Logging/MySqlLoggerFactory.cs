﻿// Copyright © 2016 Oracle and/or its affiliates. All rights reserved.
//
// MySQL Connector/NET is licensed under the terms of the GPLv2
// <http://www.gnu.org/licenses/old-licenses/gpl-2.0.html>, like most 
// MySQL Connectors. There are special exceptions to the terms and 
// conditions of the GPLv2 as it is applied to this software, see the 
// FLOSS License Exception
// <http://www.mysql.com/about/legal/licensing/foss-exception.html>.
//
// This program is free software; you can redistribute it and/or modify 
// it under the terms of the GNU General Public License as published 
// by the Free Software Foundation; version 2 of the License.
//
// This program is distributed in the hope that it will be useful, but 
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY 
// or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License 
// for more details.
//
// You should have received a copy of the GNU General Public License along 
// with this program; if not, write to the Free Software Foundation, Inc., 
// 51 Franklin St, Fifth Floor, Boston, MA 02110-1301  USA

using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace EntityFrameworkCore.Basic.Tests.Logging
{
    public class MySqlLoggerFactory : ILoggerFactory
    {
        private static MySqlLogger _logger;
        private static MySqlLogger Logger => LazyInitializer.EnsureInitialized(ref _logger);

        public static IReadOnlyList<string> SqlStatements => Logger.MySqlLoggerData.mySqlStatements;

        public static IReadOnlyList<DbCommandLogData> CommandLogData => Logger.MySqlLoggerData.logData;

        public void AddProvider(ILoggerProvider provider)
        {
            throw new NotImplementedException();
        }

        public ILogger CreateLogger(string categoryName)
        {
            return Logger;
        }

        public void Dispose()
        {            
        }

        public static string Sql => string.Join( Environment.NewLine, Logger.MySqlLoggerData.mySqlStatements);
        public static void Reset() => Logger.ResetLoggerData();


        private class MySqlLogger : ILogger
        {

#if NETCORE10
            // AsyncLocal type so the data is preserved across diferent threads.
            private readonly static AsyncLocal<MySqlLoggerData> loggerData = new AsyncLocal<MySqlLoggerData>();
#else
            private const string contextName = "_LogMySQL";
#endif

            public MySqlLoggerData MySqlLoggerData
            {
                get
                {
#if !NETCORE10
                    var loggerData = (SqlLoggerData)CallContext.LogicalGetData(ContextName);
#else
                    var lgd = loggerData.Value;

#endif
                    return lgd ?? (loggerData.Value = new MySqlLoggerData());
                }
            }


            public IDisposable BeginScope<TState>(TState state)
            {
                MySqlLoggerData.stmt = new StringBuilder();
                return new HelperDisposable();
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {

                var format = formatter(state, exception)?.Trim();
                if (format != null)
                {
                    var mysqlLoggerData = new MySqlLoggerData();
                    var cmdLog = state as DbCommandLogData;

                    if (cmdLog != null)
                    {
                        var parameters = "";

                        if (cmdLog.Parameters.Any())
                        {
                            parameters = string.Join(Environment.NewLine , cmdLog.Parameters.Select(p => $"{p.Name}: {p.Value.ToString()}"));
                        }

                        mysqlLoggerData.mySqlStatements.Add(parameters + cmdLog.CommandText);

                        mysqlLoggerData.logData.Add(cmdLog);
                    }

                    else
                    {
                        mysqlLoggerData.stmt.AppendLine(format);
                    }

                    mysqlLoggerData.xUnitOutputHelper?.WriteLine(format + Environment.NewLine);
                }
            }

            internal void ResetLoggerData()
            {
                loggerData.Value = null;
            }
        }


        private class MySqlLoggerData
        {
            public string output => stmt.ToString();

#if !NETCORE10
            [NonSerialized]
#endif
            public StringBuilder stmt = new StringBuilder();
            public readonly List<string> mySqlStatements = new List<string>();
#if !NETCORE10
            [NonSerialized]
#endif
            public readonly List<DbCommandLogData> logData = new List<DbCommandLogData>();

#if !NETCORE10
            [NonSerialized]
#endif
            public ITestOutputHelper xUnitOutputHelper;
        }

        private class HelperDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
