using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Rhino.ETL.Exceptions;

namespace Rhino.ETL
{
	[DebuggerDisplay("Assoication: {From}.{FromQueue} >> {To}.{ToQueue}")]
	public class PipelineAssociation
	{
		private string from;
		private string to;
		private string fromQueue;
		private string toQueue;
		private AssociationType fromType;
		private AssociationType toType;
		private IDictionary parameters = new Hashtable(StringComparer.InvariantCultureIgnoreCase);
		private IInput fromInstance;
		private IOutput toInstance;

		public event Action<PipelineAssociation> Completed = delegate { };

		public IDictionary Parameters
		{
			get { return parameters; }
		}

		public AssociationType FromType
		{
			get { return fromType; }
			set { fromType = value; }
		}

		public AssociationType ToType
		{
			get { return toType; }
			set { toType = value; }
		}

		public string From
		{
			get { return from; }
			set { from = value; }
		}

		public string To
		{
			get { return to; }
			set { to = value; }
		}

		public string FromQueue
		{
			get { return fromQueue; }
			set { fromQueue = value; }
		}

		public string ToQueue
		{
			get { return toQueue; }
			set { toQueue = value; }
		}

		public void Validate(ICollection<string> messages)
		{
			ValidateName(messages, From, FromType);
			ValidateName(messages, To, ToType);
		}

		private void ValidateName(ICollection<string> messages, string name, AssociationType associationType)
		{
			int associationIndex = Pipeline.Current.Associations.IndexOf(this);
			int count = 0;
			if (EtlConfigurationContext.Current.Sources.ContainsKey(name) &&
			    (associationType == AssociationType.Any || associationType == AssociationType.Sources))
				count += 1;
			if (EtlConfigurationContext.Current.Destinations.ContainsKey(name) &&
			    (associationType == AssociationType.Any || associationType == AssociationType.Destinations))
				count += 1;
			if (EtlConfigurationContext.Current.Transforms.ContainsKey(name) &&
			    (associationType == AssociationType.Any || associationType == AssociationType.Transforms))
				count += 1;
			if (EtlConfigurationContext.Current.Joins.ContainsKey(name) &&
				(associationType == AssociationType.Any || associationType == AssociationType.Joins))
				count += 1;

			if (count == 0)
			{
				messages.Add(
					string.Format("Could not find element '{0}' on association #{1} in pipeline [{2}]", name, associationIndex,
					              Pipeline.Current.Name));
			}
			if (count > 1)
			{
				messages.Add(
					string.Format(
						"Ambigious match for '{0}' on association #{1} in pipeline [{2}] - you need to qualify it with Sources.{0}, Destinations.{0} or Transforms.{0} or Joins.{0}",
						name, associationIndex, Pipeline.Current.Name));
			}
		}

		public void PerformSecondStagePass()
		{
			FromInstance = FindFromContext<IInput>(From, FromType);
			ToInstance = FindFromContext<IOutput>(To, ToType);
		}

		public IOutput ToInstance
		{
			get { return toInstance; }
			set { toInstance = value; }
		}

		public IInput FromInstance
		{
			get { return fromInstance; }
			set { fromInstance = value; }
		}

		public T FindFromContext<T>(string name, AssociationType associationType)
			where T : class
		{
			T obj = null;
			if (EtlConfigurationContext.Current.Sources.ContainsKey(name) &&
			    (associationType == AssociationType.Any || associationType == AssociationType.Sources))
			{
				obj = EtlConfigurationContext.Current.Sources[name] as T;
			}
			if (EtlConfigurationContext.Current.Destinations.ContainsKey(name) &&
			    (associationType == AssociationType.Any || associationType == AssociationType.Destinations))
			{
				obj = EtlConfigurationContext.Current.Destinations[name] as T;
			}
			if (EtlConfigurationContext.Current.Transforms.ContainsKey(name) &&
			    (associationType == AssociationType.Any || associationType == AssociationType.Transforms))
			{
				obj = EtlConfigurationContext.Current.Transforms[name] as T;
			}
			if (EtlConfigurationContext.Current.Joins.ContainsKey(name) &&
				(associationType == AssociationType.Any || associationType == AssociationType.Joins))
			{
				obj = EtlConfigurationContext.Current.Joins[name] as T;
			}
			if (obj == null)
			{
				throw new ExpectedInterfaceNotfoundException("Expected " + name + " to implement " + typeof (T).Name +
				                                             " but it doesn't.");
			}
			return obj;
		}

	    public void ConnectEnds(Pipeline pipeline)
	    {
	    	string destinationQueue = ToQueue ?? DefaultQueue;
	    	FromInstance.RegisterForwarding(
	    		new PipeLineStage(
					pipeline,
					FromQueue ?? DefaultQueue,
	    			ToInstance,
					destinationQueue, 500, Parameters)
					);
	    	ToInstance.Completed += delegate(IOutput output, string queueName)
	    	{
				if (destinationQueue.Equals(queueName,StringComparison.InvariantCultureIgnoreCase))
	    			Completed(this);
	    	};
	    }

        private const string DefaultQueue = "Output";
	}
}