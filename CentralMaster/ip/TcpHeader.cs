using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CentralMaster.ip
{
    public class TcpHeader : ProtocolHeader
    {
        public static uint Sequence_Number = 0;
        public static uint Acknowledgement_Number = 0;

        private ushort srcPort;
        private ushort destPort;
        private ushort tcpLength;
        private ushort tcpChecksum;
        private ushort windowSize;
        private uint sequence_number_recv = 0;
        private uint acknowledgement_number_recv = 0;
        public uint tcpHeaderLength_recv = 0;

        public byte NS = 0;
        public byte CWR = 0;
        public byte ECE = 0;
        public byte URG = 0;
        public byte ACK = 0;
        public byte PSH = 0;
        public byte RST = 0;
        public byte SYN = 0;
        public byte FIN = 0;

        public Ipv6Header ipv6PacketHeader;

        static public int TcpHeaderLength = 20; // 20 to 60; simple tcp header no options


        /// <summary>
        /// Simple constructor for the UDP header.
        /// </summary>
        public TcpHeader()
            : base()
        {
            srcPort = 0;
            destPort = 0;
            tcpLength = 0;
            tcpChecksum = 0;

            ipv6PacketHeader = null;
        }

        /// <summary>
        /// Gets and sets the destination port. Performs the necessary byte order conversion.
        /// </summary>
        public ushort SourcePort
        {
            get
            {
                return (ushort)IPAddress.NetworkToHostOrder((short)srcPort);
            }
            set
            {
                srcPort = (ushort)IPAddress.HostToNetworkOrder((short)value);
            }
        }

        /// <summary>
        /// Gets and sets the destination port. Performs the necessary byte order conversion.
        /// </summary>
        public ushort DestinationPort
        {
            get
            {
                return (ushort)IPAddress.NetworkToHostOrder((short)destPort);
            }
            set
            {
                destPort = (ushort)IPAddress.HostToNetworkOrder((short)value);
            }
        }

        /// <summary>
        /// Gets and sets the UDP payload length. This is the length of the payload
        /// plus the size of the UDP header itself.
        /// </summary>
        public ushort Length
        {
            get
            {
                return (ushort)IPAddress.NetworkToHostOrder((short)tcpLength);
            }
            set
            {
                tcpLength = (ushort)IPAddress.HostToNetworkOrder((short)value);
            }
        }

        /// <summary>
        /// Gets and sets the checksum value. It performs the necessary byte order conversion.
        /// </summary>
        public ushort Checksum
        {
            get
            {
                return (ushort)IPAddress.NetworkToHostOrder((short)tcpChecksum);
            }
            set
            {
                tcpChecksum = (ushort)IPAddress.HostToNetworkOrder((short)value);
            }
        }

        /// <summary>
        /// Gets and sets the windowSize value. Performs necessary byte order conversion
        /// </summary>
        public ushort WindowSize
        {
            get
            {
                return (ushort)IPAddress.NetworkToHostOrder((short)windowSize);
            }
            set
            {
                windowSize = (ushort)IPAddress.HostToNetworkOrder((short)value);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="tcpData"></param>
        /// <param name="bytesCopied"></param>
        /// <returns></returns>
        static public TcpHeader Create(byte[] tcpData, int start_index, ref int bytesCopied)
        {
            TcpHeader tcpPacketHeader = new TcpHeader();

            tcpPacketHeader.srcPort = BitConverter.ToUInt16(tcpData, start_index + 0);
            tcpPacketHeader.destPort = BitConverter.ToUInt16(tcpData, start_index + 2);
            tcpPacketHeader.sequence_number_recv = BitConverter.ToUInt32(tcpData, start_index + 4);
            tcpPacketHeader.acknowledgement_number_recv = BitConverter.ToUInt32(tcpData, start_index + 8);
            //uint tcpHeaderLen = (uint) ((tcpData[start_index + 12] & 0xF0) >> 4) * 4;
            tcpPacketHeader.NS = (byte)(tcpData[start_index + 12] & 0x01);
            if ( (tcpData[start_index + 13] & 0x80) == 0x80)
            {
                tcpPacketHeader.CWR = 1;
            }
            else
            {
                tcpPacketHeader.CWR = 0;
            }
            if ((tcpData[start_index + 13] & 0x40) == 0x40)
            {
                tcpPacketHeader.ECE = 1;
            }
            else
            {
                tcpPacketHeader.ECE = 0;
            }
            if ((tcpData[start_index + 13] & 0x20) == 0x40)
            {
                tcpPacketHeader.URG = 1;
            }
            else
            {
                tcpPacketHeader.URG = 0;
            }
            if ((tcpData[start_index + 13] & 0x10) == 0x40)
            {
                tcpPacketHeader.ACK = 1;
            }
            else
            {
                tcpPacketHeader.ACK = 0;
            }
            if ((tcpData[start_index + 13] & 0x08) == 0x08)
            {
                tcpPacketHeader.PSH = 1;
            }
            else
            {
                tcpPacketHeader.PSH = 0;
            }
            if ((tcpData[start_index + 13] & 0x04) == 0x04)
            {
                tcpPacketHeader.RST = 1;
            }
            else
            {
                tcpPacketHeader.RST = 0;
            }
            if ((tcpData[start_index + 13] & 0x02) == 0x02)
            {
                tcpPacketHeader.SYN = 1;
            }
            else
            {
                tcpPacketHeader.SYN = 0;
            }
            if ((tcpData[start_index + 13] & 0x01) == 0x01)
            {
                tcpPacketHeader.FIN = 1;
            }
            else
            {
                tcpPacketHeader.FIN = 0;
            }
            tcpPacketHeader.WindowSize =
            tcpPacketHeader.srcPort = BitConverter.ToUInt16(tcpData, start_index + 14);
            tcpPacketHeader.tcpChecksum = BitConverter.ToUInt16(tcpData, start_index + 16);
            // ignore urg pointer 2 bytes
            tcpPacketHeader.tcpHeaderLength_recv = 20;
            bytesCopied = 20;
            return tcpPacketHeader;
        }



        /// <summary>
        /// This method builds the byte array representation of the UDP header as it would appear
        /// on the wire. To do this it must build the IPv4 or IPv6 pseudo header in order to
        /// calculate the checksum on the packet. This requires knowledge of the IPv4 or IPv6 header
        /// so one of these must be set before a UDP packet can be set.
        ///
        /// The IPv4 pseudo header consists of:
        ///   4-byte source IP address
        ///   4-byte destination address
        ///   1-byte zero field
        ///   1-byte protocol field
        ///   2-byte TCP length
        ///   2-byte source port
        ///   2-byte destination port
        ///   4-byte sequence number
        ///   4-byte acknowledgement number
        ///   1-byte (0-3 bits data offset); (4-6 bits reserved vals 0) (1 bit NS bit)
        ///   1-byte (flags .. CWR .... FIN)
        ///   2-byte window size
        ///   2-byte checksum
        ///   2-byte urgent pointer
        ///   options (optional)...
        ///   TCP payload (padded to the next 32-bit boundary)
        /// The IPv6 pseudo header consists of:
        ///   16-byte source address
        ///   16-byte destination address
        ///   4-byte TCP length
        ///   3-byte zero pad
        ///   1-byte next header value
        ///   2-byte source port
        ///   2-byte destination port
        ///   4-byte sequence number
        ///   4-byte acknowledgement number
        ///   1-byte (0-3 bits data offset); (4-6 bits reserved vals 0) (1 bit NS bit)
        ///   1-byte (flags .. CWR .... FIN)
        ///   2-byte window size
        ///   2-byte checksum
        ///   2-byte urgent pointer
        ///   options (optional)...
        ///   TCP payload (padded to the next 32-bit boundary)
        /// </summary>
        /// <param name="payLoad">Payload that follows the TCP header</param>
        /// <returns></returns>
        public override byte[] GetProtocolPacketBytes(byte[] payLoad)
        {
            byte[] tcpPacket = new byte[TcpHeaderLength + payLoad.Length], pseudoHeader = null, byteValue = null;
            int offset = 0;

            // Build the TCP packet first
            byteValue = BitConverter.GetBytes(srcPort);
            Array.Copy(byteValue, 0, tcpPacket, offset, byteValue.Length);
            offset += byteValue.Length;

            byteValue = BitConverter.GetBytes(destPort);
            Array.Copy(byteValue, 0, tcpPacket, offset, byteValue.Length);
            offset += byteValue.Length;

            byteValue = BitConverter.GetBytes((uint)IPAddress.HostToNetworkOrder(TcpHeader.Sequence_Number++));
            Array.Copy(byteValue, 0, tcpPacket, offset, byteValue.Length);
            offset += byteValue.Length;

            if (this.ACK == 1)
            {
                byteValue = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(TcpHeader.Acknowledgement_Number++));
            }
            else
            {
                byteValue = new byte[4];
                byteValue[0] = 0; byteValue[1] = 0;
                byteValue[2] = 0; byteValue[3] = 0;
            }
            Array.Copy(byteValue, 0, tcpPacket, offset, byteValue.Length);
            offset += byteValue.Length;

            byte tmpb = 0x50;// data offset is 5 => 20 bytes header ; no options
            if (this.NS == 1)
            {
                tmpb = (byte) (tmpb & 0x01);
            }
            tcpPacket[offset] = tmpb;
            offset++;
            
            // flags CWR...FIN
            tmpb = 0;
            if (this.CWR == 1)
            {
                tmpb = (byte)(tmpb & 0x80);
            }
            if (this.ECE == 1)
            {
                tmpb = (byte)(tmpb & 0x40);
            }
            if (this.URG == 1)
            {
                tmpb = (byte)(tmpb & 0x20);
            }
            if (this.ACK == 1)
            {
                tmpb = (byte)(tmpb & 0x10);
            }
            if (this.PSH == 1)
            {
                tmpb = (byte)(tmpb & 0x08);
            }
            if (this.RST == 1)
            {
                tmpb = (byte)(tmpb & 0x04);
            }
            if (this.SYN == 1)
            {
                tmpb = (byte)(tmpb & 0x02);
            }
            if (this.FIN == 1)
            {
                tmpb = (byte)(tmpb & 0x01);
            }
            tcpPacket[offset] = tmpb;
            offset++;

            byteValue = BitConverter.GetBytes(windowSize);
            Array.Copy(byteValue, 0, tcpPacket, offset, byteValue.Length);
            offset += byteValue.Length;

            tcpPacket[offset++] = 0;      // Checksum is initially zero
            tcpPacket[offset++] = 0;

            // not using URG pointer set to zero: todo throw exception if URG flag is set
            tcpPacket[offset++] = 0;      
            tcpPacket[offset++] = 0;


            // Copy payload to end of packet
            Array.Copy(payLoad, 0, tcpPacket, offset, payLoad.Length);

            if (ipv6PacketHeader != null)
            {
                uint ipv6PayloadLength;

                pseudoHeader = new byte[TcpHeaderLength + 40 + payLoad.Length];

                // Build the IPv6 pseudo header
                offset = 0;

                // Source address
                byteValue = ipv6PacketHeader.SourceAddress.GetAddressBytes();
                Array.Copy(byteValue, 0, pseudoHeader, offset, byteValue.Length);
                offset += byteValue.Length;

                // Destination address
                byteValue = ipv6PacketHeader.DestinationAddress.GetAddressBytes();
                Array.Copy(byteValue, 0, pseudoHeader, offset, byteValue.Length);
                offset += byteValue.Length;

                ipv6PayloadLength = (uint)IPAddress.HostToNetworkOrder((int)(payLoad.Length + TcpHeaderLength));

                // Packet payload: ICMPv6 headers plus payload
                byteValue = BitConverter.GetBytes(ipv6PayloadLength);
                Array.Copy(byteValue, 0, pseudoHeader, offset, byteValue.Length);
                offset += byteValue.Length;

                // 3 bytes zero pad plus next header protocol value
                pseudoHeader[offset++] = 0;
                pseudoHeader[offset++] = 0;
                pseudoHeader[offset++] = 0;
                pseudoHeader[offset++] = ipv6PacketHeader.NextHeader;

                // Copy the tcp packet to the end of this
                Array.Copy(tcpPacket, 0, pseudoHeader, offset, tcpPacket.Length);
            }

            if (pseudoHeader != null)
            {
                Checksum = ComputeChecksum(pseudoHeader);
            }

            // Put checksum back into packet
            byteValue = BitConverter.GetBytes(tcpChecksum);
            Array.Copy(byteValue, 0, tcpPacket, 16, byteValue.Length);

            return tcpPacket;
        }



    }
}
