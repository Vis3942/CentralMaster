using System;

namespace CentralMaster.Communications
{
	public interface ITransmit
	{
		void transmit(byte[] to_send);
	}
}
