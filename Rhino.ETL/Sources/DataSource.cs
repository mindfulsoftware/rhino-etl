using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using Boo.Lang;
using Rhino.ETL.Exceptions;

namespace Rhino.ETL
{
	public class DataSource : BaseDataElement<DataSource>, IInput
	{
		private QueuesManager queueManager;
		private const string OutputQueueName = "Output";

		public DataSource(string name)
			: base(name)
		{
			queueManager = new QueuesManager(name, Logger);
			EtlConfigurationContext.Current.AddSource(name, this);
		}

		public void RegisterForwarding(PipeLineStage pipeLineStage)
		{
			queueManager.RegisterForwarding(pipeLineStage);
		}

		public void Start(Pipeline pipeline)
		{
			QueueKey key = new QueueKey(OutputQueueName, pipeline);
			using (IDbCommand command = GetDbConnection(pipeline).CreateCommand())
			{
				command.CommandText = Command;
				AddParameters(command);

				using (IDataReader reader = command.ExecuteReader())
				{
					DataTable schema = reader.GetSchemaTable();
					List<string> columns = new List<string>();
					foreach (DataRow schemaRow in schema.Rows)
					{
						columns.Add((string) schemaRow["ColumnName"]);
					}
					while (reader.Read())
					{
						Row row = new Row();
						for (int i = 0; i < columns.Count; i++)
						{
							object value = reader.GetValue(i);
							if (value == DBNull.Value)
								value = null;
							row[columns[i]] = value;
						}
						queueManager.Forward(key, row);
					}
				}
			}
			queueManager.Complete(key);
		}
	}
}