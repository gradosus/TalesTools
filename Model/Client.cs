using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Net;
using System.Net.Mail;

namespace _4RTools.Model
{
    public class ClientDTO
    {
        public int index { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string hpAddress { get; set; }
        public string nameAddress { get; set; }
        public string mapAddress { get; set; }
        public int hpAddressPointer { get; set; }
        public int nameAddressPointer { get; set; }
        public int mapAddressPointer { get; set; }

        public ClientDTO() { }

        public ClientDTO(string name, string description, string hpAddress, string nameAddress, string mapAddress)
        {
            this.name = name;
            this.description = description;
            this.hpAddress = hpAddress;
            this.nameAddress = nameAddress;
            this.mapAddress = mapAddress;
            this.hpAddressPointer = ParseAddress(hpAddress);
            this.nameAddressPointer = ParseAddress(nameAddress);
            this.mapAddressPointer = ParseAddress(mapAddress);
        }

        private static int ParseAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return 0;
            return Convert.ToInt32(address, 16);
        }
    }

    public sealed class ClientListSingleton
    {
        private static List<Client> clients = new List<Client>();

        public static void AddClient(Client c) { clients.Add(c); }
        public static void RemoveClient(Client c) { clients.Remove(c); }
        public static List<Client> GetAll() { return clients; }

        public static bool ExistsByProcessName(string processName)
        {
            return clients.Exists(client => string.Equals(client.processName, processName, StringComparison.OrdinalIgnoreCase));
        }
    }

    public sealed class ClientSingleton
    {
        private static Client client;
        private ClientSingleton(Client client) { ClientSingleton.client = client; }
        public static ClientSingleton Instance(Client client) { return new ClientSingleton(client); }
        public static Client GetClient() { return client; }
    }

    public class Client
    {
        public Process process { get; }
        public string processName { get; private set; }
        private Utils.ProcessMemoryReader PMR { get; set; }
        public int currentNameAddress { get; set; }
        public int currentHPBaseAddress { get; set; }
        public int currentMapAddress { get; set; }
        private int statusBufferAddress { get; set; }
        private int currentOpenChatAddress { get; set; }
        private int _num = 0;

        public Client(string processName, int currentHPBaseAddress, int currentNameAddress, int currentMapAddress)
        {
            this.currentNameAddress = currentNameAddress;
            this.currentHPBaseAddress = currentHPBaseAddress;
            this.currentMapAddress = currentMapAddress;
            this.processName = processName;
            this.statusBufferAddress = currentHPBaseAddress + 0x474;
            this.currentOpenChatAddress = 0x012A714C;
        }

        public Client(ClientDTO dto)
        {
            this.processName = dto.name;
            this.currentHPBaseAddress = ParseAddress(dto.hpAddress);
            this.currentNameAddress = ParseAddress(dto.nameAddress);
            this.currentMapAddress = ParseAddress(dto.mapAddress);
            this.statusBufferAddress = this.currentHPBaseAddress + 0x474;
            this.currentOpenChatAddress = 0x012A714C;
        }

        private static int ParseAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return 0;
            return Convert.ToInt32(address, 16);
        }

        public Client(string processName)
        {
            PMR = new Utils.ProcessMemoryReader();
            string[] parts = processName.Split(new string[] { ".exe - " }, StringSplitOptions.None);
            string rawProcessName = parts[0];
            int choosenPID = int.Parse(parts[1]);

            foreach (Process process in Process.GetProcessesByName(rawProcessName))
            {
                if (choosenPID == process.Id)
                {
                    this.process = process;
                    PMR.ReadProcess = process;
                    PMR.OpenProcess();

                    try
                    {
                        Client c = GetClientByProcess(rawProcessName);
                        if (c == null) throw new Exception();
                        this.currentHPBaseAddress = c.currentHPBaseAddress;
                        this.currentNameAddress = c.currentNameAddress;
                        this.currentMapAddress = c.currentMapAddress;
                        this.statusBufferAddress = c.statusBufferAddress;
                        this.currentOpenChatAddress = c.currentOpenChatAddress;
                    }
                    catch
                    {
                        MessageBox.Show("This client is not supported. Only spammers and macros will work.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        this.currentHPBaseAddress = 0;
                        this.currentNameAddress = 0;
                        this.currentMapAddress = 0;
                        this.statusBufferAddress = 0;
                        this.currentOpenChatAddress = 0;
                    }
                }
            }
        }

        private string ReadMemoryAsString(int address)
        {
            if (address == 0) return string.Empty;
            byte[] bytes = PMR.ReadProcessMemory((IntPtr)address, 40u, out _num);
            List<byte> buffer = new List<byte>();
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == 0) break;
                buffer.Add(bytes[i]);
            }
            return Encoding.Default.GetString(buffer.ToArray());
        }

        private uint ReadMemory(int address)
        {
            if (address == 0) return 0;
            return BitConverter.ToUInt32(PMR.ReadProcessMemory((IntPtr)address, 4u, out _num), 0);
        }

        public byte ReadMemoryAsByte(int address)
        {
            if (address == 0) return 0;
            byte[] bytes = PMR.ReadProcessMemory((IntPtr)address, 1, out _num);
            return bytes[0];
        }
        public void WriteMemory(int address, uint intToWrite) { PMR.WriteProcessMemory((IntPtr)address, BitConverter.GetBytes(intToWrite), out _num); }
        public void WriteMemory(int address, byte[] bytesToWrite) { PMR.WriteProcessMemory((IntPtr)address, bytesToWrite, out _num); }
        public bool IsHpBelow(int percent) { return ReadCurrentHp() * 100 < percent * ReadMaxHp(); }
        public bool IsSpBelow(int percent) { return ReadCurrentSp() * 100 < percent * ReadMaxSp(); }
        public bool IsHpAbove(int percent) { return ReadCurrentHp() * 100 > percent * ReadMaxHp(); }
        public bool IsSpAbove(int percent) { return ReadCurrentSp() * 100 > percent * ReadMaxSp(); }
        public uint ReadCurrentHp() { return ReadMemory(this.currentHPBaseAddress); }
        public uint ReadCurrentSp() { return ReadMemory(this.currentHPBaseAddress + 8); }
        public uint ReadMaxHp() { return ReadMemory(this.currentHPBaseAddress + 4); }
        public string ReadCharacterName() { return ReadMemoryAsString(this.currentNameAddress); }
        public string ReadCurrentMap() { return ReadMemoryAsString(this.currentMapAddress); }
        public bool ReadOpenChat() { return Convert.ToBoolean(ReadMemoryAsByte(this.currentOpenChatAddress)); }
        public uint ReadMaxSp() { return ReadMemory(this.currentHPBaseAddress + 12); }
        public uint CurrentBuffStatusCode(int effectStatusIndex) { return ReadMemory(this.statusBufferAddress + effectStatusIndex * 4); }

        public Client GetClientByProcess(string processName)
        {
            foreach (Client c in ClientListSingleton.GetAll())
            {
                if (string.Equals(c.processName, processName, StringComparison.OrdinalIgnoreCase))
                {
                    uint hpBaseValue = ReadMemory(c.currentHPBaseAddress);
                    if (hpBaseValue > 0) return c;
                }
            }
            return null;
        }

        public static Client FromDTO(ClientDTO dto)
        {
            return ClientListSingleton.GetAll()
                .Where(c => string.Equals(c.processName, dto.name, StringComparison.OrdinalIgnoreCase))
                .Where(c => c.currentHPBaseAddress == dto.hpAddressPointer)
                .Where(c => c.currentNameAddress == dto.nameAddressPointer)
                .FirstOrDefault();
        }
    }
}
