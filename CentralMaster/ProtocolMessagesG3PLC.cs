using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LogUtils;

namespace CentralMaster
{
    public class ProtocolMessagesG3PLC
    {
        #region consts
        public const ushort CAPABILITY_REQUEST_ID = 0x0001;
        public const ushort CAPABILITY_RESPONSE_ID = 0x0002;
        public const ushort DATA_REQUEST_ID = 0x0003;
        public const ushort DATA_RESPONSE_ID = 0x0004;
        public const ushort COMMAND_REQUEST_ID = 0x0005;
        public const ushort COMMAND_RESPONSE_ID = 0x0006;
        public const ushort SIGNATURE_REQUEST_ID = 0x0007;
        public const ushort SIGNATURE_RESPONSE_ID = 0x0008;
        public const ushort PUBLICKEY_REQUEST_ID = 0x0009;
        public const ushort PUBLICKEY_RESPONSE_ID = 0x000a;
        public const ushort SESSION_REQUEST_ID = 0x000b;
        public const ushort SESSION_RESPONSE_ID = 0x000c;
        public const ushort RESET_REQUEST_ID = 0x000d;
        public const ushort RESET_RESPONSE_ID = 0x000e;
        public const ushort COMMON_NAME_REQUEST_ID = 0X000f;
        public const ushort COMMON_NAME_RESPONSE_ID = 0X0010;
        public const ushort SYNC_TIME_REQUEST_ID = 0x0011;
        public const ushort SYNC_TIME_RESPONSE_ID = 0x0012;
        public const ushort SCHEDULE_COMMAND_ID = 0x0013;
        public const ushort SCHEDULE_RESPONSE_ID = 0x0014;
        public const ushort BRIDGENODE_DATA_REQUEST_ID = 0x001F;
        public const ushort BRIDGENODE_DATA_RESPONSE_ID = 0x0020;
        
        public const ushort BRIDGENODE_COMMAND_REQUEST_ID = 0x0021;
        public const ushort BRIDGENODE_COMMAND_RESPONSE_ID = 0x0022;
        public const ushort RS232_COMMAND_ID = 0x0017;
        public const ushort RS232_RESPONSE_ID = 0x0018;
        public const ushort RS485_COMMAND_ID = 0x0019;
        public const ushort RS485_RESPONSE_ID = 0x001A;
        public const ushort DALI_COMMAND_ID = 0x001B;
        public const ushort DALI_RESPONSE_ID = 0x001C;
        public const ushort DIGITAL_INPUT_REQUEST_ID = 0x001D;
        public const ushort DIGITAL_INPUT_RESPONSE_ID = 0x001E;
        public const ushort DIGITAL_OUTPUT_COMMAND_ID = 0x0025;
        public const ushort DIGITAL_OUTPUT_RESPONSE_ID = 0x0026;
        public const ushort POWER_MEASUREMENT_REQUEST_ID = 0x0027;
        public const ushort POWER_MEASUREMENT_RESPONSE_ID = 0x0028;
        public const ushort ANALOG_INPUT_REQUEST_ID = 0x0029;
        public const ushort ANALOG_INPUT_RESPONSE_ID = 0x002A;
        public const ushort ANALOG_OUTPUT_COMMAND_ID = 0x002B;
        public const ushort ANALOG_OUTPUT_RESPONSE_ID = 0x002C;
        public const ushort PROTOCOL_CONFIRM_COMMAND_ID = 0x002D;
        public const ushort PROTOCOL_CONFIRM_RESPONSE_ID = 0x002E;
        public const ushort I2C_COMMAND_ID = 0x002F;
        public const ushort I2C_RESPONSE_ID = 0x0030;
        public const ushort IAQ_REQUEST_ID = 0x0031;
        public const ushort IAQ_RESPONSE_ID = 0x0032;



        // note: CPB => short form for Capability
        public const ushort CPB_ONOFF = 0x8001; // supports setting values (bit 15 is  1)
        public const ushort CPB_INTENSITY = 0x8002; // supports setting values (bit 15 is  1)
        public const ushort CPB_RGB = 0x8003; // supports setting values (bit 15 is  1)
        public const ushort CPB_ACTIVE_POWER = 0x0004; // does not support setting values (bit 15 is  0)
        public const ushort CPB_CUMULATIVE_ENERGY = 0x0005; // does not support setting values (bit 15 is  0)
        public const ushort CPB_VOLTAGE = 0x0006; // does not support setting values (bit 15 is  0)
        public const ushort CPB_MONITORING_STATUS = 0x0007; // does not support setting values (bit 15 is  0)
        public const ushort CPB_PERSON_DETECT = 0x0008; // does not support setting values (bit 15 is  0)
        public const ushort CPB_TEMPERATURE = 0x0009; // does not support setting values (bit 15 is  0)
        public const ushort CPB_HUMIDITY = 0x000a; // does not support setting values (bit 15 is  0)
        public const ushort CPB_PRESSURE = 0x000b; // does not support setting values (bit 15 is  0)
        public const ushort CPB_AMBIENT_LIGHT = 0x000c; // does not support setting values (bit 15 is  0)
        public const ushort CPB_VOLATILE_ORGANIC = 0x000d; // does not support setting values (bit 15 is  0)
        public const ushort CPB_C02 = 0x000e; // does not support setting values (bit 15 is  0)
        public const ushort CPB_MOTION_DETECT = 0x000f; // does not support setting values (bit 15 is  0)
        public const ushort CPB_REPORT_INTERVAL = 0x8011; // supports settings values (bit 15 is  1) in seconds
        public const ushort CPB_LINE_CURRENT = 0x0012; // does not support setting values (bit 15 is  0)

        public const byte DATA_LEN_ONOFF = 0x01;
        public const byte DATA_LEN_INTENSITY = 0x02;
        public const byte DATA_LEN_RGB = 0x03;
        public const byte DATA_LEN_ACTIVE_POWER = 0x04; // as float
        public const byte DATA_LEN_CUMULATIVE_ENERGY = 0x04; // as float
        public const byte DATA_LEN_VOLTAGE = 0x04; // as float
        public const byte DATA_LEN_MONITORING_STATUS = 0x01;
        public const byte DATA_LEN_PERSON_DETECT = 0x01;
        public const byte DATA_LEN_TEMPERATURE = 0x04; // as float
        public const byte DATA_LEN_HUMIDITY = 0x04; // as float
        public const byte DATA_LEN_PRESSURE = 0x04; // as float
        public const byte DATA_LEN_AMBIENT_LIGHT = 0x04; // as float
        public const byte DATA_LEN_VOLATILE_ORGANIC = 0x02;
        public const byte DATA_LEN_C02 = 0x02;
        public const byte DATA_LEN_MOTION_DETECT = 0x01;
        public const byte DATA_LEN_REPORT_INTERVAL = 0x04; // as uint32
        public const byte DATA_LEN_LINE_CURRENT = 0x04; // as float
        public const ushort DEVICE_CAPABILITY_LENGTH = 15;
        public static ushort[] device_capability =
        {
            CPB_ONOFF,
            CPB_INTENSITY,
            CPB_ACTIVE_POWER,
            CPB_CUMULATIVE_ENERGY,
            CPB_VOLTAGE,
            CPB_MONITORING_STATUS,
            CPB_TEMPERATURE,
            CPB_HUMIDITY,
            CPB_PRESSURE,
            CPB_AMBIENT_LIGHT,
            CPB_VOLATILE_ORGANIC,
            CPB_C02,
            CPB_MOTION_DETECT,
            CPB_REPORT_INTERVAL,
            CPB_LINE_CURRENT
        };

        public static byte[] device_capability_datalen =
        {
            DATA_LEN_ONOFF,
            DATA_LEN_INTENSITY,
            DATA_LEN_ACTIVE_POWER,
            DATA_LEN_CUMULATIVE_ENERGY,
            DATA_LEN_VOLTAGE,
            DATA_LEN_MONITORING_STATUS,
            DATA_LEN_TEMPERATURE,
            DATA_LEN_HUMIDITY,
            DATA_LEN_PRESSURE,
            DATA_LEN_AMBIENT_LIGHT,
            DATA_LEN_VOLATILE_ORGANIC,
            DATA_LEN_C02,
            DATA_LEN_MOTION_DETECT,
            DATA_LEN_REPORT_INTERVAL,
            DATA_LEN_LINE_CURRENT
        };

        #endregion // consts

        public ProtocolMessagesG3PLC()
        {
            // constructor
        }

        public static ushort CapabilityRequestPacketLen()
        {
            ushort length = 2 + 2 + 2; // total len (2 bytes), message_id (2 bytes), length of bytes -> 2bytes 
            return length;
        }

        public static void CapabilityRequestPacket(byte[] pkt, ushort pkt_len)
        {
            // NO ERROR CHECKING; PKT_LEN MUST BE 6; call CapabilityRequestPacketLen before calling this function to get the pkt len
            pkt[0] = (byte)pkt_len;
            pkt[1] = (byte)(pkt_len >> 8);
            pkt[2] = (byte)CAPABILITY_REQUEST_ID;
            pkt[3] = (byte)(CAPABILITY_REQUEST_ID >> 8);
            pkt[4] = 0;
            pkt[5] = 0;
        }

        public static ushort CapabilityResponsePacketLen()
        {
            ushort length = 2 + 2; // total len (2 bytes), message_id (2 bytes)
            length += 2; // number of ids in capability response packet as ushort
            length += (2 * DEVICE_CAPABILITY_LENGTH);
            return length;
        }


        public static void CapabilityResponsePacket(byte[] pkt, ushort pkt_len)
        {
            byte ii;
            byte jj;
            ushort tmpu16;
            pkt[0] = (byte)pkt_len;
            pkt[1] = (byte)(pkt_len >> 8);
            pkt[2] = (byte)CAPABILITY_RESPONSE_ID;
            pkt[3] = (byte)(CAPABILITY_RESPONSE_ID >> 8);
            pkt[4] = (byte)DEVICE_CAPABILITY_LENGTH;
            pkt[5] = (byte)(DEVICE_CAPABILITY_LENGTH >> 8);
            jj = 6;
            for (ii = 0; ii < DEVICE_CAPABILITY_LENGTH; ii++)
            {
                tmpu16 = device_capability[ii];
                pkt[jj++] = (byte)tmpu16;
                pkt[jj++] = (byte)(tmpu16 >> 8);
            }
        }

        // returns data length for a particular cmd_id, else returns 0 if cmd_id not found in the device_capability array
        public static ushort GetDataLengthForDeviceId(ushort cmd_id)
        {
            byte cmd_index;
            ushort cmd_data_len;
            cmd_index = 0;
            while ((cmd_index < (byte)DEVICE_CAPABILITY_LENGTH) && (device_capability[cmd_index] != cmd_id))
            {
                cmd_index++;
            }
            if (cmd_index < (byte)DEVICE_CAPABILITY_LENGTH)
            {
                cmd_data_len = device_capability_datalen[cmd_index];
            }
            else
            {
                // did not find cmd id in the device capability array; so ignore it
                cmd_data_len = 0;
            }
            return cmd_data_len;
        }

        public static ushort CommandRequestPacketLen(ushort[] cmd_ids, ushort cmd_ids_len)
        {
            ushort length = 2 + 2; // total len (2 bytes), message_id (2 bytes)
            byte ii;
            length += 2; // number of subtypes
            ushort tmp_len;
            for (ii = 0; ii < cmd_ids_len; ii++)
            {
                tmp_len = GetDataLengthForDeviceId(cmd_ids[ii]);
                if (tmp_len > 0)
                {
                    length += 2; // for id value
                    length += tmp_len;
                }
            }
            return length;
        }

        // initializes the packet and returns the start location where cmd id and data can be appended
        public static byte CommandRequestPacketInit(byte[] pkt, ushort pkt_len)
        {
            // NO ERROR CHECKING; PKT_LEN MUST BE 4; call CapabilityRequestPacketLen before calling this function to get the pkt len
            pkt[0] = (byte)0x06; // this will be updated on each append command
            pkt[1] = (byte)(0x00); // this will be updated on each append command
            pkt[2] = (byte)COMMAND_REQUEST_ID;
            pkt[3] = (byte)(COMMAND_REQUEST_ID >> 8);
            pkt[4] = 0x00; // this will be updated on each append command
            pkt[5] = 0x00; // this will be updated on each append command
            return 6;
        }

        // appends the cmd_id and corresponding data and return the next available location for the next append
        // if return value of function == start_index; given cmd_id cound not be found
        public static byte CommandRequestPacketAppend(byte[] pkt, ushort pkt_len, byte insert_at, ushort cmd_id, byte[] cmd_val)
        {
            byte tmp_st_index = insert_at;
            ushort tmp_len = GetDataLengthForDeviceId(cmd_id);
            ushort tmp_u16;
            byte ii;
            if (tmp_len > 0) // valid command id found
            {
                pkt[tmp_st_index++] = (byte)cmd_id;
                pkt[tmp_st_index++] = (byte)(cmd_id >> 8);
                for (ii = 0; ii < tmp_len; ii++)
                {
                    pkt[tmp_st_index++] = cmd_val[ii];
                    //cmd_val++;
                }
                // update pkt_len
                tmp_u16 = tmp_len;
                tmp_u16 += (ushort)(pkt[0] | (ushort)(pkt[1] << 8) );
                tmp_u16 += 2;
                pkt[0] = (byte)tmp_u16;
                pkt[1] = (byte)(tmp_u16 >> 8);
                // update number of items
                tmp_u16 = 1;
                tmp_u16 += (ushort)(pkt[4] | (ushort)(pkt[5] << 8));
                pkt[4] = (byte)tmp_u16;
                pkt[5] = (byte)(tmp_u16 >> 8);
            }
            return tmp_st_index;
        }

        public static ushort CommandResponsePacketLen(ushort num_responses)
        {
            ushort length = 2 + 2; // total len (2 bytes), message_id (2 bytes)
            length += 2; // number of responses
            length += (ushort)(3 * num_responses);
            return length;

        }

        public static void CommandResponsePacket(byte[] pkt, ushort pkt_len, byte[] status, ushort[] cmd_ids_resp, ushort num_responses)
        {
            ushort tmpu16;
            byte ii, jj;
            pkt[0] = (byte)pkt_len;
            pkt[1] = (byte)(pkt_len >> 8);
            pkt[2] = (byte)COMMAND_RESPONSE_ID;
            pkt[3] = (byte)(COMMAND_RESPONSE_ID >> 8);
            pkt[4] = (byte)num_responses;
            pkt[5] = (byte)(num_responses >> 8);
            jj = 6;
            for (ii = 0; ii < DEVICE_CAPABILITY_LENGTH; ii++)
            {
                tmpu16 = cmd_ids_resp[ii];
                pkt[jj++] = (byte)tmpu16;
                pkt[jj++] = (byte)(tmpu16 >> 8);
                pkt[jj++] = status[ii];
            }
        }



        public static ushort DataRequestPacketLen()
        {
            ushort length = 2 + 2 + 2; // total len (2 bytes), message_id (2 bytes), length bytes -> 2 bytes
            return length;
        }

        public static void DataRequestPacket(byte[] pkt, ushort pkt_len)
        {
            // NO ERROR CHECKING; PKT_LEN MUST BE 6; call DataRequestPacketLen before calling this function to get the pkt len
            pkt[0] = (byte)pkt_len;
            pkt[1] = (byte)(pkt_len >> 8);
            pkt[2] = (byte)DATA_REQUEST_ID;
            pkt[3] = (byte)(DATA_REQUEST_ID >> 8);
            pkt[4] = 0;
            pkt[5] = 0;
        }


        // unknown cpb_ids are ignored and not used in the computation
        public static ushort DataResponsePacketLen(ushort[] cpb_ids, ushort cpb_ids_len)
        {
            ushort length = 2 + 2; // total len (2 bytes), message_id (2 bytes)
            byte ii;
            length += 2; // number of subtypes
            ushort tmp_len;
            for (ii = 0; ii < cpb_ids_len; ii++)
            {
                tmp_len = GetDataLengthForDeviceId(cpb_ids[ii]);
                if (tmp_len > 0)
                {
                    length += 2; // for id value
                    length += tmp_len;
                }
            }
            return length;
        }

        public static byte DataResponsePacketInit(byte[] pkt, ushort pkt_len)
        {
            // NO ERROR CHECKING; call DataResponsePacketLen before calling this function to get the pkt len
            pkt[0] = (byte)0x06; // this will be updated on each append command
            pkt[1] = (byte)(0x00); // this will be updated on each append command
            pkt[2] = (byte)DATA_RESPONSE_ID;
            pkt[3] = (byte)(DATA_RESPONSE_ID >> 8);
            pkt[4] = 0x00; // this will be updated on each append command
            pkt[5] = 0x00; // this will be updated on each append command
            return 6;
        }

        public static byte DataResponsePacketAppend(byte[] pkt, ushort pkt_len, byte insert_at, ushort cpb_id, byte[] cpb_id_value)
        {
            byte tmp_st_index = insert_at;
            ushort tmp_len = GetDataLengthForDeviceId(cpb_id);
            ushort tmp_u16;
            byte ii;
            if (tmp_len > 0) // valid command id found
            {
                pkt[tmp_st_index++] = (byte)cpb_id;
                pkt[tmp_st_index++] = (byte)(cpb_id >> 8);
                for (ii = 0; ii < tmp_len; ii++)
                {
                    pkt[tmp_st_index++] = cpb_id_value[ii];
                    //cpb_id_value++;
                }
                // update pkt_len
                tmp_u16 = tmp_len;
                tmp_u16 += (ushort)(pkt[0] | (ushort)(pkt[1] << 8));
                tmp_u16 += 2;
                pkt[0] = (byte)tmp_u16;
                pkt[1] = (byte)(tmp_u16 >> 8);
                // update number of items
                tmp_u16 = 1;
                tmp_u16 += (ushort)(pkt[4] | (ushort)(pkt[5] << 8)); ;
                pkt[4] = (byte)tmp_u16;
                pkt[5] = (byte)(tmp_u16 >> 8);
            }
            return tmp_st_index;
        }


        public static ushort SignatureBytesRequestPacketLen()
        {
            ushort length = 2 + 2 + 2; // total len (2 bytes), message_id (2 bytes), 2 bytes length field which will be 0x0000
            return length;
        }


        public static void SignatureBytesRequestPacket(byte[] pkt, ushort pkt_len)
        {
            pkt[0] = (byte)pkt_len;
            pkt[1] = (byte)(pkt_len >> 8);
            pkt[2] = (byte)SIGNATURE_REQUEST_ID;
            pkt[3] = (byte)(SIGNATURE_REQUEST_ID >> 8);
            pkt[4] = 0;
            pkt[5] = 0;
        }


        public static ushort SignatureBytesResponsePacketLen(ushort signature_bytes_len)
        {
            ushort length = 2 + 2; // total len (2 bytes), message_id (2 bytes)
            length += 2; // number of signature bytes as uint16_6
            length += signature_bytes_len; // actual length of signature bytes
            return length;
        }

        public static void SignatureBytesResponsePacket(byte[] pkt, ushort pkt_len, byte[] signature_bytes, ushort signature_bytes_len)
        {
            byte ii;
            //byte* ptr_sigresp;
            pkt[0] = (byte)pkt_len;
            pkt[1] = (byte)(pkt_len >> 8);
            pkt[2] = (byte)SIGNATURE_RESPONSE_ID;
            pkt[3] = (byte)(SIGNATURE_RESPONSE_ID >> 8);
            pkt[4] = (byte)signature_bytes_len;
            pkt[5] = (byte)(signature_bytes_len >> 8);
            //ptr_sigresp = (byte*)signature_bytes;
            for (ii = 0; ii < signature_bytes_len; ii++)
            {
                pkt[6 + ii] = signature_bytes[ii]; // *ptr_sigresp;
                //ptr_sigresp++;
            }
        }

        public static ushort PublicKeyRequestPacketLen()
        {
            ushort length = 2 + 2 + 2; // total len (2 bytes), message_id (2 bytes), 2 bytes length which will always be 0x0000
            return length;
        }


        public static void PublicKeyRequestPacket(byte[] pkt, ushort pkt_len)
        {
            pkt[0] = (byte)pkt_len;
            pkt[1] = (byte)(pkt_len >> 8);
            pkt[2] = (byte)PUBLICKEY_REQUEST_ID;
            pkt[3] = (byte)(PUBLICKEY_REQUEST_ID >> 8);
            pkt[4] = 0;
            pkt[5] = 0;
        }

        public static ushort PublicKeyResponsePacketLen(ushort public_key_len)
        {
            ushort length = 2 + 2; // total len (2 bytes), message_id (2 bytes)
            length += 2; // number of signature bytes as uint16_6
            length += public_key_len; // actual length of signature bytes
            return length;
        }

        public static void PublicKeyResponsePacket(byte[] pkt, ushort pkt_len, byte[] public_key_bytes, ushort public_key_len)
        {
            byte ii;
            //byte* ptr_pubkeyb;
            pkt[0] = (byte)pkt_len;
            pkt[1] = (byte)(pkt_len >> 8);
            pkt[2] = (byte)PUBLICKEY_RESPONSE_ID;
            pkt[3] = (byte)(PUBLICKEY_RESPONSE_ID >> 8);
            pkt[4] = (byte)public_key_len;
            pkt[5] = (byte)(public_key_len >> 8);
            //ptr_pubkeyb = (byte*)public_key_bytes;
            for (ii = 0; ii < public_key_len; ii++)
            {
                pkt[6 + ii] = public_key_bytes[ii];// *ptr_pubkeyb;
                //ptr_pubkeyb++;
            }

        }

        public static ushort SessionRequestPacketLen(ushort public_key_len)
        {
            ushort length = 2 + 2; // total len (2 bytes), message_id (2 bytes)
            length += 2; // number of signature bytes as uint16_6
            length += public_key_len; // actual length of signature bytes
            return length;
        }

        public void SessionRequestPacket(byte[] pkt, ushort pkt_len, byte[]public_key_bytes, ushort public_key_len)
        {
            byte ii;
            //byte* ptr_pubkeyb;
            pkt[0] = (byte)pkt_len;
            pkt[1] = (byte)(pkt_len >> 8);
            pkt[2] = (byte)SESSION_REQUEST_ID;
            pkt[3] = (byte)(SESSION_REQUEST_ID >> 8);
            pkt[4] = (byte)public_key_len;
            pkt[5] = (byte)(public_key_len >> 8);
            //ptr_pubkeyb = (byte*)public_key_bytes;
            for (ii = 0; ii < public_key_len; ii++)
            {
                pkt[6 + ii] = public_key_bytes[ii]; // *ptr_pubkeyb;
                //ptr_pubkeyb++;
            }

        }

        public static ushort SessionResponsePacketLen(ushort public_key_len)
        {
            ushort length = 2 + 2; // total len (2 bytes), message_id (2 bytes)
            length += 2; // number of signature bytes as uint16_6
            length += public_key_len; // actual length of signature bytes
            return length;
        }

        public static void SessionResponsePacket(byte[] pkt, ushort pkt_len, byte[]public_key_bytes, ushort public_key_len)
        {
            byte ii;
            //byte* ptr_pubkeyb;
            pkt[0] = (byte)pkt_len;
            pkt[1] = (byte)(pkt_len >> 8);
            pkt[2] = (byte)SESSION_RESPONSE_ID;
            pkt[3] = (byte)(SESSION_RESPONSE_ID >> 8);
            pkt[4] = (byte)public_key_len;
            pkt[5] = (byte)(public_key_len >> 8);
            //ptr_pubkeyb = (byte*)public_key_bytes;
            for (ii = 0; ii < public_key_len; ii++)
            {
                pkt[6 + ii] = public_key_bytes[ii]; // *ptr_pubkeyb;
                //ptr_pubkeyb++;
            }
        }


        public static ushort ResetRequestPacketLen()
        {
            ushort length = 2 + 2 + 2; //total bytes, id bytes, and message length bytes ->2, will always be 0x0000
            return length;
        }

        public static void ResetRequestPacket(byte[] pkt, ushort pkt_len)
        {
            // NO ERROR CHECKING; PKT_LEN MUST BE 6; call len function before calling this function to get the pkt len
            pkt[0] = (byte)pkt_len;
            pkt[1] = (byte)(pkt_len >> 8);
            pkt[2] = (byte)RESET_REQUEST_ID;
            pkt[3] = (byte)(RESET_REQUEST_ID >> 8);
            pkt[4] = 0;
            pkt[5] = 0;
        }

        public static ushort ResetResponsePacketLen()
        {
            ushort length = 2 + 2 + 2; //total bytes, id bytes, and message length bytes ->2, will always be 0x0000
            return length;
        }

        public static void ResetResponsePacket(byte[] pkt, ushort pkt_len)
        {
            // NO ERROR CHECKING; PKT_LEN MUST BE 6; call len function before calling this function to get the pkt len
            pkt[0] = (byte)pkt_len;
            pkt[1] = (byte)(pkt_len >> 8);
            pkt[2] = (byte)RESET_RESPONSE_ID;
            pkt[3] = (byte)(RESET_RESPONSE_ID >> 8);
            pkt[4] = 0;
            pkt[5] = 0;
        }


        public static ushort CommonNameRequestPacketLen()
        {
            ushort length = 2 + 2 + 2; //total bytes, id bytes, and message length bytes ->2, will always be 0x0000
            return length;
        }

        public static ushort ProtocolConfirmCommandPacketLen()
        {
            ushort length = 2 + 2 + 2; //total bytes, id bytes, and message length bytes ->2, will always be 0x0000
            return length;
        }

        public static void ProtocolConfirmCommandPacket(byte[] pkt, ushort pkt_len)
        {
            // NO ERROR CHECKING; PKT_LEN MUST BE 6; call len function before calling this function to get the pkt len
            pkt[0] = (byte)pkt_len;
            pkt[1] = (byte)(pkt_len >> 8);
            pkt[2] = (byte)PROTOCOL_CONFIRM_COMMAND_ID;
            pkt[3] = (byte)(PROTOCOL_CONFIRM_COMMAND_ID>> 8);
            pkt[4] = 0;
            pkt[5] = 0;

        }

        public static void CommonNameRequestPacket(byte[] pkt, ushort pkt_len)
        {
            // NO ERROR CHECKING; PKT_LEN MUST BE 6; call len function before calling this function to get the pkt len
            pkt[0] = (byte)pkt_len;
            pkt[1] = (byte)(pkt_len >> 8);
            pkt[2] = (byte)COMMON_NAME_REQUEST_ID;
            pkt[3] = (byte)(COMMON_NAME_REQUEST_ID >> 8);
            pkt[4] = 0;
            pkt[5] = 0;

        }

        public static ushort CommonNameResponsePacketLen(ushort common_name_len)
        {
            ushort length = 2 + 2; // total len (2 bytes), message_id (2 bytes)
            length += 2; // number of signature bytes as uint16_6
            length += common_name_len; // actual length of bytes
            return length;
        }

        public static void CommonNameResponsePacket(byte[] pkt, ushort pkt_len, byte[] common_name_bytes, ushort common_name_len)
        {
            byte ii;
            //byte* ptr_common_name;
            pkt[0] = (byte)pkt_len;
            pkt[1] = (byte)(pkt_len >> 8);
            pkt[2] = (byte)COMMON_NAME_RESPONSE_ID;
            pkt[3] = (byte)(COMMON_NAME_RESPONSE_ID >> 8);
            pkt[4] = (byte)common_name_len;
            pkt[5] = (byte)(common_name_len >> 8);
            //ptr_common_name = (byte*)common_name_bytes;
            for (ii = 0; ii < common_name_len; ii++)
            {
                pkt[6 + ii] = common_name_bytes[ii]; // *ptr_common_name;
                //ptr_common_name++;
            }
        }


        public static ushort SyncTimeRequestPacketLen()
        {
            ushort length = 2 + 2; // total len (2 bytes), message_id (2 bytes)
            length += 14; // 14 bytes for time (data formats v 16)
            length += 4; // added uint for rrlooptime
            return length;
        }

        public static void SyncTimeRequestPacket(byte[] pkt, ushort pkt_len, byte[] sync_time_bytes, ushort sync_time_len, uint rr_time_secs)
        {
            // sync_time_len should be of length 14 bytes; 
            // NO error checking done
            byte ii;
            pkt[0] = (byte)pkt_len;
            pkt[1] = (byte)(pkt_len >> 8);
            pkt[2] = (byte)SYNC_TIME_REQUEST_ID;
            pkt[3] = (byte)(SYNC_TIME_REQUEST_ID >> 8);
            for (ii = 0; ii < sync_time_len; ii++)
            {
                pkt[4 + ii] = sync_time_bytes[ii];
            }
            byte[] tmpb = BitConverter.GetBytes(rr_time_secs);
            pkt[4 + ii] = tmpb[0];
            pkt[4 + ii + 1] = tmpb[1];
            pkt[4 + ii + 2] = tmpb[2];
            pkt[4 + ii + 3] = tmpb[3];
        }

        public static ushort SyncTimeResponsePacketLen()
        {
            ushort length = 2 + 2; // total len (2 bytes), message_id (2 bytes)
            length += 1; // 1 status byte for time response
            return length;
        }

        public static void SyncTimeResponsePacket(byte[] pkt, ushort pkt_len, byte status)
        {
            //byte ii;
            pkt[0] = (byte)pkt_len;
            pkt[1] = (byte)(pkt_len >> 8);
            pkt[2] = (byte)SYNC_TIME_RESPONSE_ID;
            pkt[3] = (byte)(SYNC_TIME_RESPONSE_ID >> 8);
            pkt[4] = status;
        }

        public static ushort ScheduleCommandPacketLen(ushort num_schedule_rows)
        {
            ushort length = 2 + 2; // total len (2 bytes), message_id (2 bytes)
            length += 2; // number of num_schedule_rows as uint16_6
            length = (ushort)(length + 7 * num_schedule_rows); // actual length of bytes
            return length;

        }

        public static void ScheduleCommandPacket(byte[] pkt, ushort pkt_len, byte[] schedule_bytes, ushort schedule_bytes_len, ushort number_schedule_rows)
        {
            byte ii;
            pkt[0] = (byte)pkt_len;
            pkt[1] = (byte)(pkt_len >> 8);
            pkt[2] = (byte)SCHEDULE_COMMAND_ID;
            pkt[3] = (byte)(SCHEDULE_COMMAND_ID >> 8);
            pkt[4] = (byte)number_schedule_rows;
            pkt[5] = (byte)(number_schedule_rows >> 8);
            for (ii = 0; ii < schedule_bytes_len; ii++)
            {
                pkt[6 + ii] = schedule_bytes[ii];
            }
        }

        public static ushort ScheduleResponsePacketLen()
        {
            ushort length = 2 + 2; // total len (2 bytes), message_id (2 bytes)
            length += 1; // 1 status byte for time response
            return length;
        }

        public static void ScheduleResponsePacket(byte[] pkt, ushort pkt_len, byte status)
        {
            //byte ii;
            pkt[0] = (byte)pkt_len;
            pkt[1] = (byte)(pkt_len >> 8);
            pkt[2] = (byte)SCHEDULE_RESPONSE_ID;
            pkt[3] = (byte)(SCHEDULE_RESPONSE_ID >> 8);
            pkt[4] = status;
        }


        // only required for processing messages on the received end

        // parses the response recevied to extract array length information
        public static ushort GetCapabilityResponseArrayLen(byte[] pkt, ushort pkt_len)
        {
            // returns the number of capability elements in the packet pkt
            ushort array_length;
            if (pkt_len <= 4) // no number of elements item in the packet 
                return 0;

            array_length = (ushort)( pkt[4] & (ushort)(pkt[5] << 8 ) );
            if (pkt_len < ((array_length + 3) * 2)) // insufficient elements as indicated by number of array elements in packet
                return 0;

            return array_length;
        }


        // returns 1 on success; 0 on failure 
        public static byte GetCapabilityResponseArray(ushort[] capability_ids, ushort capability_ids_len, byte[] pkt, ushort pkt_len)
        {
            // call this after calling GetCapabilityResponseLen returns a value > 0
            ushort ii;
            //byte* ptr_arr = &pkt[6];
            ushort tmp_len = 6;
            for (ii = 0; ii < capability_ids_len; ii++)
            {
                capability_ids[ii] = (ushort)(pkt[6 + ii] & (ushort) (pkt[6+1+ii] << 8) ); //*(ushort*)ptr_arr;
                //ptr_arr += 2;
                tmp_len += 2;
                if (tmp_len >= pkt_len)
                {
                    // invalid condition, not enough data to extract from packet
                    return 0;
                }
            }

            return 1;
        }

        public static ushort RS232CommandPacketLenV1_1(byte[] data_in_cmd)
        {
            ushort length = 2 + 2; // total len (2 bytes), message_id (2 bytes)
            length += ((ushort)data_in_cmd.Length); 
            return length;
        }

        public static void RS232CommandPacketV1_1(byte[] pkt, ushort pkt_len, byte[] data_in_cmd)
        {
            pkt[0] = (byte)pkt_len;
            pkt[1] = (byte)(pkt_len >> 8);
            pkt[2] = (byte)RS232_COMMAND_ID;
            pkt[3] = (byte)(RS232_COMMAND_ID >> 8);
            Array.Copy(data_in_cmd, 0, pkt, 4, data_in_cmd.Length);
        }

        public static ushort RS232ResponsePacketLenV1_1(ushort len)
        {
            ushort length = (ushort)(2 + 2 + 1+ 1+ len + 1); // total len (2 bytes), message_id (2 bytes), command type 1 byte , address 1 byte, message lenght + 1 byte status
            return length;
        }

        public static void RS232ResponsePacketV1_1(byte[] pkt, ushort pkt_len, byte[] RS232ResponseBytes, ushort RS232_Response_len, byte status)
        {

            byte ii;
            //header
            pkt[0] = (byte)pkt_len;
            pkt[1] = (byte)(pkt_len >> 8);
            pkt[2] = (byte)RS232_RESPONSE_ID;
            pkt[3] = (byte)(RS232_RESPONSE_ID >> 8);
            //data:
            pkt[4] = 1; //command type is 1
            pkt[5] = 0; //address is always zero for rs232
            pkt[6] = status;
            for (ii = 0; ii < RS232_Response_len; ii++)
            {
                pkt[7 + ii] = RS232ResponseBytes[ii];
            }

        }

		public static ushort AppendPacket(byte[] pkt, ushort pkt_len, bool isAppend, byte[] to_add_pkt, ushort to_add_pkt_len)
		{
			ushort tmp_add_len;
			ushort tmp_curr_len;
			
			tmp_add_len = to_add_pkt[0];
			tmp_add_len = (ushort)(tmp_add_len + (to_add_pkt[1] << 8));
			if (!isAppend)
			{
				// first packet being added
				if ( (tmp_add_len+2) > pkt_len )
				{
					// cannot add packet 
					return 0;
				}
				pkt[0] = (byte)(tmp_add_len+2);
				pkt[1] = (byte)((tmp_add_len+2)>>8);
                Array.Copy(to_add_pkt,0, pkt, 2, tmp_add_len);
				return (ushort)(tmp_add_len+2);
			}
			else 
			{
				tmp_curr_len = pkt[0];
				tmp_curr_len = (ushort)(tmp_curr_len + (pkt[1] << 8));
                DCLogger.Information(string.Format("curr len {0}: add len {1}, combopktlen {2}", tmp_curr_len, tmp_add_len, pkt_len));
				if ( (tmp_curr_len + tmp_add_len) > pkt_len )
				{
					return 0;
				}
                Array.Copy(to_add_pkt, 0, pkt, tmp_curr_len, tmp_add_len);
				tmp_curr_len = (ushort)(tmp_curr_len + tmp_add_len);
				pkt[0] = (byte) tmp_curr_len;
				pkt[1] = (byte) (tmp_curr_len >> 8);
				return tmp_curr_len;
			}
		}

        /// <summary>
        ///  Generate base packet for the following messages
        ///     RS232 Command, RS485 Command, DALI Command, 
        ///     RS232 Response, RS485 Response, DALI Response
        ///     Digital Input Request, Digital Input Response, Digital Output Command, Digital Output Response
        ///     Power Measurement Request, Power Measurement Response
        ///     Analog Input Request, Analog Input Response, Analog Output Command, Analog Output Response
        /// </summary>
        /// <param name="pkt_id"></param>
        /// <param name="pkt"></param>
        /// <param name="pkt_size"></param>
        /// <returns>
        /// returns 0xFFFFF 
        ///     -> unsupported packet ids
        ///     -> insufficient size for packet
        /// else returns length of packet (details populated)
        /// </returns>
        public static ushort GeneratePacketBase(ushort pkt_id, ref byte[] pkt, int pkt_size)
        {
            bool supp_pkt_id = false;
            int tmp_len = 2 + 2 + 1; // pkt len = 2; msg id = 2; number of channels = 1
            if (tmp_len >= pkt_size)
            {
                return (0xffff);
            }
            pkt[0] = (byte)(tmp_len);
            pkt[1] = (byte)((tmp_len) >> 8);
            // pkt_id supported in this function are:
            // RS232_COMMAND_ID, RS485_COMMAND_ID, DALI_COMMAND_ID, RS232_RESPONSE_ID, RS485_RESPONSE_ID, DALI_RESPONSE_ID,
            // DIGITAL_INPUT_REQUEST_ID , DIGITAL_INPUT_RESPONSE_ID, DIGITAL_OUTPUT_COMMAND_ID, DIGITAL_OUTPUT_RESPONSE_ID
            // POWER_MEASUREMENT_REQUEST_ID, POWER_MEASUREMENT_RESPONSE_ID
            // ANALOG_INPUT_REQUEST_ID, ANALOG_INPUT_RESPONSE_ID, ANALOG_OUTPUT_COMMAND_ID, ANALOG_OUTPUT_RESPONSE_ID,
            // IAQ_REQUEST_ID
            switch (pkt_id)
            {
                case RS232_COMMAND_ID:
                case RS485_COMMAND_ID:
                case DALI_COMMAND_ID:
                case RS232_RESPONSE_ID:
                case RS485_RESPONSE_ID:
                case DALI_RESPONSE_ID:
                case DIGITAL_INPUT_REQUEST_ID:
                case DIGITAL_INPUT_RESPONSE_ID:
                case DIGITAL_OUTPUT_COMMAND_ID:
                case DIGITAL_OUTPUT_RESPONSE_ID:
                case POWER_MEASUREMENT_REQUEST_ID:
                case POWER_MEASUREMENT_RESPONSE_ID:
                case ANALOG_INPUT_REQUEST_ID:
                case ANALOG_INPUT_RESPONSE_ID:
                case ANALOG_OUTPUT_COMMAND_ID:
                case ANALOG_OUTPUT_RESPONSE_ID:
                case I2C_COMMAND_ID:
                case IAQ_REQUEST_ID:
                    supp_pkt_id = true;
                    break;
                default:
                    supp_pkt_id = false;
                    break;

            }

            if (supp_pkt_id == false)
            {
                return 0xFFFF;
            }
            pkt[2] = (byte)pkt_id;
            pkt[3] = (byte)(pkt_id >> 8);
            // number of channels = 0 (just creating packet, need to add data with additional functions
            pkt[4] = 0;
            return (ushort)tmp_len;
        }


        /// <summary>
        ///  Use this functin to Add channel command data for the following messages
        ///     RS232 Command, RS485 Command, DALI Command, 
        ///  after having generated Base packet using the GeneratePacketBase(....) function
        /// </summary>
        /// <param name="pkt_type"></param>
        /// <param name="pkt"></param>
        /// <param name="pkt_size"></param>
        /// <param name="channel_number"></param>
        /// <param name="msg_type"></param>
        /// <param name="address"></param>
        /// <param name="ch_data"></param>
        /// <param name="ch_data_len"></param>
        /// <returns>
        /// returns 0xFFFFF 
        ///     -> unsupported packet ids
        ///     -> insufficient size for packet
        /// else returns length of packet (details populated)
        /// </returns>
        public static ushort AddToPacketSerialCommand(ushort pkt_id, ref byte[] pkt, int pkt_size, int channel_number, 
            byte msg_type, byte address, byte[] ch_data, int ch_data_len)
        {
            bool is_correct_type = false;
            switch (pkt_id)
            {
                case RS232_COMMAND_ID:
                    if ( (pkt[2] == (byte)RS232_COMMAND_ID) && (pkt[3] == (byte)(RS232_COMMAND_ID >> 8)) )
                    {
                        is_correct_type = true;
                    }
                    break;
                case RS485_COMMAND_ID:
                    if ((pkt[2] == (byte)RS485_COMMAND_ID) && (pkt[3] == (byte)(RS485_COMMAND_ID >> 8)))
                    {
                        is_correct_type = true;
                    }
                    break;
                case DALI_COMMAND_ID:
                    if ((pkt[2] == (byte)DALI_COMMAND_ID) && (pkt[3] == (byte)(DALI_COMMAND_ID >> 8)))
                    {
                        is_correct_type = true;
                    }
                    break;
                case I2C_COMMAND_ID:
                    if ((pkt[2] == (byte)I2C_COMMAND_ID) && (pkt[3] == (byte)(I2C_COMMAND_ID >> 8)))
                    {
                        is_correct_type = true;
                    }
                    break;
                default:
                    // unsupported or unknown type
                    is_correct_type = false;
                    break;
            }
            if (!is_correct_type)
            {
                return 0xFFFF;
            }
            else
            {
                int tmp_curr_len;
                tmp_curr_len = pkt[0];
                tmp_curr_len = (ushort)(tmp_curr_len + (pkt[1] << 8));
                if ((tmp_curr_len + 3 + ch_data_len) >= pkt_size)
                {
                    return 0xFFFF;
                }
                pkt[4]++; //increment channel count
                // fill in data
                pkt[tmp_curr_len] = (byte)channel_number;
                if (msg_type == 0)
                {
                    pkt[tmp_curr_len + 1] = 0;
                }
                else
                {
                    pkt[tmp_curr_len + 1] = 1;
                }
                pkt[tmp_curr_len + 2] = address;
                pkt[tmp_curr_len + 3] = (byte)ch_data_len;
                Array.Copy(ch_data, 0, pkt, tmp_curr_len + 4, ch_data_len);
                tmp_curr_len += (4 + ch_data_len); // channel number -> 1 byte; cmd type address, adddress, channel data len (data in that channel)
                pkt[0] = (byte)(tmp_curr_len);
                pkt[1] = (byte)((tmp_curr_len) >> 8);
                return (ushort)tmp_curr_len;
            }
        }

        /// <summary>
        ///  Use this functin to Add channel response data for the following message types
        ///     RS232 Response, RS485 Response, DALI Response, 
        ///  after having generated Base packet using the GeneratePacketBase(....) function
        /// </summary>
        /// <param name="pkt_type"></param>
        /// <param name="pkt"></param>
        /// <param name="pkt_size"></param>
        /// <param name="channel_number"></param>
        /// <param name="msg_type"></param>
        /// <param name="address"></param>
        /// <param name="status"></param>
        /// <param name="resp_data"></param>
        /// <param name="resp_data_len"></param>
        /// <returns>
        /// returns 0xFFFFF 
        ///     -> unsupported packet ids
        ///     -> insufficient size for packet
        /// else returns length of packet (details populated)
        /// </returns>
        public static ushort AddToPacketSerialResponse(ushort pkt_id, ref byte[] pkt, int pkt_size, int channel_number,
            byte msg_type, byte address, byte status, byte[] resp_data, int resp_data_len)
        {
            bool is_correct_type = false;
            switch (pkt_id)
            {
                case RS232_RESPONSE_ID:
                    if ((pkt[2] == (byte)RS232_RESPONSE_ID) && (pkt[3] == (byte)(RS232_RESPONSE_ID >> 8)))
                    {
                        is_correct_type = true;
                    }
                    break;
                case RS485_RESPONSE_ID:
                    if ((pkt[2] == (byte)RS485_RESPONSE_ID) && (pkt[3] == (byte)(RS485_RESPONSE_ID >> 8)))
                    {
                        is_correct_type = true;
                    }
                    break;
                case DALI_RESPONSE_ID:
                    if ((pkt[2] == (byte)DALI_RESPONSE_ID) && (pkt[3] == (byte)(DALI_RESPONSE_ID >> 8)))
                    {
                        is_correct_type = true;
                    }
                    break;
                case I2C_RESPONSE_ID:
                    if ((pkt[2] == (byte)I2C_RESPONSE_ID) && (pkt[3] == (byte)(I2C_RESPONSE_ID >> 8)))
                    {
                        is_correct_type = true;
                    }
                    break;

                default:
                    // unknown type
                    is_correct_type = false;
                    break;
            }
            if (!is_correct_type)
            {
                return 0xFFFF;
            }
            else
            {
                int tmp_curr_len;
                tmp_curr_len = pkt[0];
                tmp_curr_len = (ushort)(tmp_curr_len + (pkt[1] << 8));
                if ((tmp_curr_len + 5 + resp_data_len) >= pkt_size)
                {
                    return 0xFFFF;
                }
                pkt[4]++; //increment channel count
                // fill in data
                pkt[tmp_curr_len] = (byte)channel_number;
                if (msg_type == 0)
                {
                    pkt[tmp_curr_len + 1] = 0;
                }
                else
                {
                    pkt[tmp_curr_len + 1] = 1;
                }
                pkt[tmp_curr_len + 2] = address;
                pkt[tmp_curr_len + 3] = status;
                pkt[tmp_curr_len + 4] = (byte) resp_data_len;
                Array.Copy(resp_data, 0, pkt, tmp_curr_len + 5, resp_data_len);
                tmp_curr_len += (5 + resp_data_len);  // channel number -> 1 byte, message type (1), address (1), status (1), data_len(1), data(data_len)
                pkt[0] = (byte)(tmp_curr_len);
                pkt[1] = (byte)((tmp_curr_len) >> 8);
                return (ushort)tmp_curr_len;
            }
        }

        public static byte[] GenerateByteArrayFromRS232ConfigString(string config_str)
        {
            // format "<speed>, <num bits>, <parity values: N [None], O[Odd], E[Even], <num stop bits>, delimiter byte, 0/1 (xon_xoff disable/enable)"
            //For Parity N => 0,  E => 1, O => 2
            string[] data_str = config_str.Split(',');
            if (data_str.Length != 6)
            {
                DCLogger.Error(string.Format("expected config format \"baud rate, num bits, parity(N/E/O), stopbits,delimiter char as int, 0/1 (disable/enable SW flow control xon_xoff\", got {0}",
                    config_str));
                DCLogger.Error(string.Format("Cannot parse rs232 config"));
                return null;
            }
            byte[] data_2_send = new byte[11];
            // config uint8 =0, address_uint8, 
            // Baud_uint32, parity_uint8, { 0 = none, 1 = even, 2 = odd }, 
            //     numberOfBits_uint8, numberOfStopBits_uint8, delimiter _uint8 { 0x10 or 0x13 typically}
            data_2_send[0] = 0; // it is config data
            data_2_send[1] = 0; // address_uint8
            byte[] tmpb = BitConverter.GetBytes((Convert.ToUInt32(data_str[0])));
            data_2_send[2] = tmpb[0];
            data_2_send[3] = tmpb[1];
            data_2_send[4] = tmpb[2];
            data_2_send[5] = tmpb[3];
            if (data_str[1].ToLower().StartsWith("n")) // parity
            {
                data_2_send[6] = 0;
            }
            else if (data_str[1].ToLower().StartsWith("e"))
            {
                data_2_send[6] = 1;
            }
            else if (data_str[1].ToLower().StartsWith("o"))
            {
                data_2_send[6] = 2;
            }
            else
            {
                // invalid parity setting
                DCLogger.Error("Invalid parity setting, expected N,E or O");
                return null;
            }
            data_2_send[7] = Convert.ToByte(data_str[2]); // num bits
            data_2_send[8] = Convert.ToByte(data_str[3]); // num stop bits
            data_2_send[9] = Convert.ToByte(data_str[4]); // delimiter
            data_2_send[10] = Convert.ToByte(data_str[5]); // buffer size if delimiter is 0x00
            return data_2_send;
        }
        /// <summary>
        /// Use this function to add Digital, Analog, Power Measurement requests to base packet
        /// after having generated Base packet using the GeneratePacketBase(....) function 
        /// </summary>
        /// <param name="pkt_type"></param>
        /// <param name="pkt"></param>
        /// <param name="pkt_size"></param>
        /// <param name="channel_number"></param>
        /// <returns>
        /// returns 0xFFFFF 
        ///     -> unsupported packet ids
        ///     -> insufficient size for packet
        /// else returns length of packet (details populated)
        /// </returns>
        public static ushort AddToPacketDigitalAnalogPowerMeasIAQRequest(ushort pkt_id, ref byte[] pkt, int pkt_size, int channel_number)
        {
            // supported packet types in this function
            // "digital_in_req", "analog_in_req", "power_meas_req"
            bool is_correct_type = false;
            switch (pkt_id)
            {
                case DIGITAL_INPUT_REQUEST_ID:
                    if ((pkt[2] == (byte)DIGITAL_INPUT_REQUEST_ID) && (pkt[3] == (byte)(DIGITAL_INPUT_REQUEST_ID >> 8)))
                    {
                        is_correct_type = true;
                    }
                    break;
                case ANALOG_INPUT_REQUEST_ID:
                    if ((pkt[2] == (byte)ANALOG_INPUT_REQUEST_ID) && (pkt[3] == (byte)(ANALOG_INPUT_REQUEST_ID >> 8)))
                    {
                        is_correct_type = true;
                    }
                    break;
                case POWER_MEASUREMENT_REQUEST_ID:
                    if ((pkt[2] == (byte)POWER_MEASUREMENT_REQUEST_ID) && (pkt[3] == (byte)(POWER_MEASUREMENT_REQUEST_ID >> 8)))
                    {
                        is_correct_type = true;
                    }
                    break;
                case IAQ_REQUEST_ID:
                    if ((pkt[2] == (byte)IAQ_REQUEST_ID) && (pkt[3] == (byte)(IAQ_REQUEST_ID >> 8)))
                    {
                        is_correct_type = true;
                    }
                    break;
                default:
                    // unknown type
                    is_correct_type = false;
                    break;
            }
            if (!is_correct_type)
            {
                return 0xFFFF;
            }
            else
            {
                int tmp_curr_len;
                tmp_curr_len = pkt[0];
                tmp_curr_len = (ushort)(tmp_curr_len + (pkt[1] << 8));
                if ((tmp_curr_len + 1) >= pkt_size)
                {
                    return 0xFFFF;
                }
                pkt[4]++; // increment channel count
                // fill in data
                pkt[tmp_curr_len] = (byte)channel_number;
                tmp_curr_len++;  // channel number -> 1 byte
                pkt[0] = (byte)(tmp_curr_len);
                pkt[1] = (byte)((tmp_curr_len) >> 8);
                return (ushort)tmp_curr_len;
            }

        }

        /// <summary>
        ///  Use this functin to Add channel analog input response data to base packet
        ///  after having generated Base packet using the GeneratePacketBase(....) function
        /// </summary>
        /// <param name="pkt"></param>
        /// <param name="pkt_size"></param>
        /// <param name="channel_number"></param>
        /// <param name="ch_value"></param>
        /// <returns>
        /// returns 0xFFFFF 
        ///     -> unsupported packet ids
        ///     -> insufficient size for packet
        /// else returns length of packet (details populated)
        /// </returns>
        public static ushort AddToPacketAnalogInputResponse(ref byte[] pkt, int pkt_size, int channel_number, float ch_value)
        {
            // it is analog in response_packet
            if ((pkt[2] == (byte)ANALOG_INPUT_RESPONSE_ID) && (pkt[3] == (byte)(ANALOG_INPUT_RESPONSE_ID >> 8)))
            {
                int tmp_curr_len;
                tmp_curr_len = pkt[0];
                tmp_curr_len = (ushort)(tmp_curr_len + (pkt[1] << 8));
                if ((tmp_curr_len + 1 + 4) >= pkt_size)
                {
                    return 0xFFFF;
                }
                pkt[4]++; // increment channel count
                // fill in data
                pkt[tmp_curr_len] = (byte)channel_number;
                byte[] tmp_b = BitConverter.GetBytes(ch_value);
                Array.Copy(tmp_b, 0, pkt, tmp_curr_len + 1, 4);
                tmp_curr_len += (1 + 4);  // channel number -> 1 byte, 4 bytes float
                pkt[0] = (byte)(tmp_curr_len);
                pkt[1] = (byte)((tmp_curr_len) >> 8);
                return (ushort)tmp_curr_len;
            }
            else
            {
                return 0xFFFF;
            }
        }

        /// <summary>
        ///  Use this functin to Add channel digital input response data to base packet
        ///  after having generated Base packet using the GeneratePacketBase(....) function
        /// </summary>
        /// <param name="pkt"></param>
        /// <param name="pkt_size"></param>
        /// <param name="channel_number"></param>
        /// <param name="ch_value"></param>
        /// <returns>
        /// returns 0xFFFFF 
        ///     -> unsupported packet ids
        ///     -> insufficient size for packet
        /// else returns length of packet (details populated)
        /// </returns>
        public static ushort AddToPacketDigitalInputResponse(ref byte[] pkt, int pkt_size, int channel_number, byte ch_value)
        {
            // it is analog in response_packet
            if ((pkt[2] == (byte)DIGITAL_INPUT_RESPONSE_ID) && (pkt[3] == (byte)(DIGITAL_INPUT_RESPONSE_ID >> 8)))
            {
                int tmp_curr_len;
                tmp_curr_len = pkt[0];
                tmp_curr_len = (ushort)(tmp_curr_len + (pkt[1] << 8));
                if ((tmp_curr_len + 1 + 4) >= pkt_size)
                {
                    return 0xFFFF;
                }
                pkt[4]++; //increment channel count
                // fill in data
                pkt[tmp_curr_len] = (byte)channel_number;
                if (ch_value == 0)
                {
                    pkt[tmp_curr_len] = 0;
                }
                else
                {
                    pkt[tmp_curr_len] = 1;
                }
                tmp_curr_len += (1 + 1);  // channel number -> 1 byte, 1 byte value (0 or 1)
                pkt[0] = (byte)(tmp_curr_len);
                pkt[1] = (byte)((tmp_curr_len) >> 8);
                return (ushort)tmp_curr_len;
            }
            else
            {
                return 0xFFFF;
            }
        }

        /// <summary>
        ///  Use this functin to Add channel analog output command data to base packet
        ///  after having generated Base packet using the GeneratePacketBase(....) function
        /// </summary>
        /// <param name="pkt"></param>
        /// <param name="pkt_size"></param>
        /// <param name="channel_number"></param>
        /// <param name="ch_value"></param>
        /// <returns>
        /// returns 0xFFFFF 
        ///     -> unsupported packet ids
        ///     -> insufficient size for packet
        /// else returns length of packet (details populated)
        /// </returns>
        public static ushort AddToPacketAnalogOutputCommand(ref byte[] pkt, int pkt_size, int channel_number, float ch_value)
        {
            // it is analog in response_packet
            if ((pkt[2] == (byte)ANALOG_OUTPUT_COMMAND_ID) && (pkt[3] == (byte)(ANALOG_OUTPUT_COMMAND_ID >> 8)))
            {
                int tmp_curr_len;
                tmp_curr_len = pkt[0];
                tmp_curr_len = (ushort)(tmp_curr_len + (pkt[1] << 8));
                if ((tmp_curr_len + 1 + 4) >= pkt_size)
                {
                    return 0xFFFF;
                }
                pkt[4]++; //increment channel count
                // fill in data
                pkt[tmp_curr_len] = (byte)channel_number;
                byte[] tmp_b = BitConverter.GetBytes(ch_value);
                Array.Copy(tmp_b, 0, pkt, tmp_curr_len + 1, 4);
                tmp_curr_len += (1 + 4);  // channel number -> 1 byte, 4 bytes float
                pkt[0] = (byte)(tmp_curr_len);
                pkt[1] = (byte)((tmp_curr_len) >> 8);
                return (ushort)tmp_curr_len;
            }
            else
            {
                return 0xFFFF;
            }
        }

        /// <summary>
        ///  Use this functin to Add channel analog output response data to base packet
        ///  after having generated Base packet using the GeneratePacketBase(....) function
        /// </summary>
        /// <param name="pkt"></param>
        /// <param name="pkt_size"></param>
        /// <param name="channel_number"></param>
        /// <param name="status"></param>
        /// <returns>
        /// returns 0xFFFFF 
        ///     -> unsupported packet ids
        ///     -> insufficient size for packet
        /// else returns length of packet (details populated)
        /// </returns>
        public static ushort AddToPacketAnalogOutputResponse(ref byte[] pkt, int pkt_size, int channel_number, byte status)
        {
            // it is analog in response_packet
            if ((pkt[2] == (byte)ANALOG_OUTPUT_RESPONSE_ID) && (pkt[3] == (byte)(ANALOG_OUTPUT_RESPONSE_ID >> 8)))
            {
                int tmp_curr_len;
                tmp_curr_len = pkt[0];
                tmp_curr_len = (ushort)(tmp_curr_len + (pkt[1] << 8));
                if ((tmp_curr_len + 1 + 4) >= pkt_size)
                {
                    return 0xFFFF;
                }
                pkt[4]++; //increment channel count
                // fill in data
                pkt[tmp_curr_len] = (byte)channel_number;
                if (status == 0)
                {
                    pkt[tmp_curr_len] = 0;
                }
                else
                {
                    pkt[tmp_curr_len] = 1;
                }
                tmp_curr_len += (1 + 1);  // channel number -> 1 byte, 1 byte value (0 or 1)
                pkt[0] = (byte)(tmp_curr_len);
                pkt[1] = (byte)((tmp_curr_len) >> 8);
                return (ushort)tmp_curr_len;
            }
            else
            {
                return 0xFFFF;
            }
        }

        /// <summary>
        ///  Use this functin to Add channel digital output command data to base packet
        ///  after having generated Base packet using the GeneratePacketBase(....) function
        /// </summary>
        /// <param name="pkt"></param>
        /// <param name="pkt_size"></param>
        /// <param name="channel_number"></param>
        /// <param name="cmd_val"></param>
        /// <returns>
        /// returns 0xFFFFF 
        ///     -> unsupported packet ids
        ///     -> insufficient size for packet
        /// else returns length of packet (details populated)
        /// </returns>
        public static ushort AddToPacketDigitalOutputCommand(ref byte[] pkt, int pkt_size, int channel_number, byte cmd_val)
        {
            // it is analog in response_packet
            if ((pkt[2] == (byte)DIGITAL_OUTPUT_COMMAND_ID) && (pkt[3] == (byte)(DIGITAL_OUTPUT_COMMAND_ID >> 8)))
            {
                int tmp_curr_len;
                tmp_curr_len = pkt[0];
                tmp_curr_len = (ushort)(tmp_curr_len + (pkt[1] << 8));
                if ((tmp_curr_len + 1 + 4) >= pkt_size)
                {
                    return 0xFFFF;
                }
                pkt[4]++; //increment channel count
                // fill in data
                pkt[tmp_curr_len] = (byte)channel_number;
                if (cmd_val == 0)
                {
                    pkt[tmp_curr_len] = 0;
                }
                else
                {
                    pkt[tmp_curr_len] = 1;
                }
                tmp_curr_len += (1 + 1);  // channel number -> 1 byte, 1 byte value (0 or 1)
                pkt[0] = (byte)(tmp_curr_len);
                pkt[1] = (byte)((tmp_curr_len) >> 8);
                return (ushort)tmp_curr_len;
            }
            else
            {
                return 0xFFFF;
            }
        }

        /// <summary>
        ///  Use this functin to Add channel digital output response data to base packet
        ///  after having generated Base packet using the GeneratePacketBase(....) function
        /// </summary>
        /// <param name="pkt"></param>
        /// <param name="pkt_size"></param>
        /// <param name="channel_number"></param>
        /// <param name="status"></param>
        /// <returns>
        /// returns 0xFFFFF 
        ///     -> unsupported packet ids
        ///     -> insufficient size for packet
        /// else returns length of packet (details populated)
        /// </returns>
        public static ushort AddToPacketDigitalOutputResponse(ref byte[] pkt, int pkt_size, int channel_number, byte status)
        {
            // it is analog in response_packet
            if ((pkt[2] == (byte)DIGITAL_OUTPUT_RESPONSE_ID) && (pkt[3] == (byte)(DIGITAL_OUTPUT_RESPONSE_ID >> 8)))
            {
                int tmp_curr_len;
                tmp_curr_len = pkt[0];
                tmp_curr_len = (ushort)(tmp_curr_len + (pkt[1] << 8));
                if ((tmp_curr_len + 1 + 4) >= pkt_size)
                {
                    return 0xFFFF;
                }
                pkt[4]++; //increment channel count
                // fill in data
                pkt[tmp_curr_len] = (byte)channel_number;
                if (status == 0)
                {
                    pkt[tmp_curr_len] = 0;
                }
                else
                {
                    pkt[tmp_curr_len] = 1;
                }
                tmp_curr_len += (1 + 1);  // channel number -> 1 byte, 1 byte value (0 or 1)
                pkt[0] = (byte)(tmp_curr_len);
                pkt[1] = (byte)((tmp_curr_len) >> 8);
                return (ushort)tmp_curr_len;
            }
            else
            {
                return 0xFFFF;
            }
        }

        /// <summary>
        ///  Use this functin to Add channel power measurement response data to base packet
        ///  after having generated Base packet using the GeneratePacketBase(....) function
        /// </summary>
        /// <param name="pkt"></param>
        /// <param name="pkt_size"></param>
        /// <param name="channel_number"></param>
        /// <param name="voltage"></param>
        /// <param name="current"></param>
        /// <param name="power_fac"></param>
        /// <param name="line_freq"></param>
        /// <param name="active_power"></param>
        /// <param name="on_off"></param>
        /// <param name="brightness_pc"></param>
        /// <returns>
        /// returns 0xFFFFF 
        ///     -> unsupported packet ids
        ///     -> insufficient size for packet
        /// else returns length of packet (details populated)
        /// </returns>
        public static ushort AddToPacketPowerMeasResponse(ref byte[] pkt, int pkt_size, int channel_number, 
            float voltage, float current, float power_fac, float line_freq, float active_power, 
            byte on_off, byte brightness_pc)
        {
            // it is analog in response_packet
            if ((pkt[2] == (byte)POWER_MEASUREMENT_RESPONSE_ID) && (pkt[3] == (byte)(POWER_MEASUREMENT_RESPONSE_ID >> 8)))
            {
                int tmp_curr_len;
                tmp_curr_len = pkt[0];
                tmp_curr_len = (ushort)(tmp_curr_len + (pkt[1] << 8));
                if ((tmp_curr_len + 1 + 4 * 4) >= pkt_size)
                {
                    return 0xFFFF;
                }
                pkt[4]++; //increment channel count
                // fill in data
                pkt[tmp_curr_len] = (byte)channel_number;
                byte[] tmp_b = BitConverter.GetBytes(voltage);
                Array.Copy(tmp_b, 0, pkt, tmp_curr_len + 1, 4);
                tmp_b = BitConverter.GetBytes(current);
                Array.Copy(tmp_b, 0, pkt, tmp_curr_len + 5, 4);
                tmp_b = BitConverter.GetBytes(power_fac);
                Array.Copy(tmp_b, 0, pkt, tmp_curr_len + 9, 4);
                tmp_b = BitConverter.GetBytes(line_freq);
                Array.Copy(tmp_b, 0, pkt, tmp_curr_len + 13, 4);
                tmp_b = BitConverter.GetBytes(active_power);
                Array.Copy(tmp_b, 0, pkt, tmp_curr_len + 17, 4);
                pkt[tmp_curr_len + 21] = on_off;
                pkt[tmp_curr_len + 22] = brightness_pc;
                tmp_curr_len += (1 + 5*4 + 2);  // channel number -> 1 byte, 5 float vals => each 4 bytes, 2 byte vals => 1 byte each 
                pkt[0] = (byte)(tmp_curr_len);
                pkt[1] = (byte)((tmp_curr_len) >> 8);
                return (ushort)tmp_curr_len;
            }
            else
            {
                return 0xFFFF;
            }
        }


    }
}
