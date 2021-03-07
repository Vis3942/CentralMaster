using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralMaster.DataServiceCommunications
{
    public interface IDCDataServiceComms
    {
        public void TransmitToDataService(string message);
    }
}
