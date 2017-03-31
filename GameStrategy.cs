using System;
using HoMM;
using HoMM.ClientClasses;

namespace Homm.Client
{
	public abstract class GameStrategy
	{
		protected HommSensorData sensorData;

		protected GameStrategy(HommSensorData currentSensorData)
		{
			sensorData = currentSensorData;
		}

		protected abstract HommSensorData ReleaseStrategy();
	}

	public class ResourseCollector : GameStrategy
	{
		public ResourseCollector(HommSensorData sensorData) : base(sensorData)
		{
		}

		protected override HommSensorData ReleaseStrategy()
		{
			return sensorData;
		}
	}
}