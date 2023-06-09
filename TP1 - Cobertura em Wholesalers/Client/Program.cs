﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Aula_2___Sockets___Client {
    internal class Program {
        public enum StatusCode {
            OK = 100,
            ERROR = 300,
            BYE = 400
        }
        static void Main(string[] args) {

            TcpClient ClientSocket = ConnectServer();

            if (ClientSocket.Connected) {
                Console.WriteLine($"Connected to: {((IPEndPoint)ClientSocket.Client.RemoteEndPoint).Address}:{((IPEndPoint)ClientSocket.Client.RemoteEndPoint).Port}");

                byte[] buffer = new byte[1024];
                string data;

                data = GetDataFromStream(ClientSocket);

                //Se a mensagem for diferente de ok deu erro na transmissão
                if (!data.Contains($"{(int)StatusCode.OK} - {StatusCode.OK}"))
                {
                    Console.WriteLine($"Error: Expected '{(int)StatusCode.OK} - {StatusCode.OK}' \nClosing connection...");
                    ClientSocket.Client.Shutdown(SocketShutdown.Both);
                    ClientSocket.Close();

                    Console.ReadKey();
                    return;
                }

                //Path do documento
                string path;
                do {
                    Console.Write("Full Path to File or Drag an Drop File: ");
                    path = Console.ReadLine();
                    path = path.Replace("\"", "");
                    path = path.Replace(@"\\", @"\");
                } while (!SendFile(ClientSocket, path));

                data = GetDataFromStream(ClientSocket);

                Console.WriteLine(data);

                data = GetDataFromStream(ClientSocket);

                Console.WriteLine(data);

                //quit
                buffer = Encoding.UTF8.GetBytes("QUIT\0\0\0");
                ClientSocket.GetStream().Write(buffer, 0, buffer.Length);

                data = GetDataFromStream(ClientSocket);

                Console.WriteLine(data);


                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();

            }
            ClientSocket.Close();

        }


        /// <summary>
        /// Receber resposta do server
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        public static string GetDataFromStream(TcpClient client) {
            byte[] buffer = new byte[1024];
            StringBuilder data = new StringBuilder();
            do {
                int byte_count = client.GetStream().Read(buffer, 0, buffer.Length);
                data.Append(Encoding.UTF8.GetString(buffer, 0, byte_count));
            } while (!data.ToString().Contains("\0\0\0"));

            return data.ToString();
        }

        /// <summary>
        /// Conectar a um servidor
        /// </summary>
        /// <returns></returns>
        public static TcpClient ConnectServer() {
            TcpClient ClientSocket = new TcpClient();

            string[] ipBuffer;
            while (true) {
                Console.WriteLine("Write the IP you want to connect to:");
                ipBuffer = Console.ReadLine().Split(':');
                if (ipBuffer.Length == 2) {
                    if (!String.IsNullOrEmpty(ipBuffer[0]) && !String.IsNullOrEmpty(ipBuffer[1]))
                        break;
                }
            }

            try {
                ClientSocket.Connect(ipBuffer[0], int.Parse(ipBuffer[1]));
            } catch (Exception e) {
                Console.WriteLine($"{e.Message}\n");
                Console.ReadKey();
            }


            return ClientSocket;
        }


        /// <summary>
        /// enviar ficheiro para o servidor
        /// </summary>
        /// <param name="ClientSocket"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool SendFile(TcpClient ClientSocket, string path) {
            try {

                Console.WriteLine($"Sending File: {path}");
                var buff = Encoding.UTF8.GetBytes(File.ReadAllText(path, Encoding.GetEncoding(1252)) + "\0\0\0");
                ClientSocket.GetStream().Write(buff, 0, buff.Length);
                return true;

            } catch (FileNotFoundException e) {

                Console.WriteLine(e.Message);
                return false;
            }
        }

    }
}
