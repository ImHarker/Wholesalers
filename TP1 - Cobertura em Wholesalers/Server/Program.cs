﻿using Aula_2___Sockets;
using Aula_2___Sockets.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Aula_2___Sockets___Server {


    class Program {
        public static dataContext dataContext = new dataContext();
        public static Mutex mutex = new Mutex();
        public enum StatusCode {
            OK = 100,
            ERROR = 300,
            BYE = 400
        }

        public enum FileStatus {
            OPEN,
            ERROR,
            IN_PROGRESS,
            COMPLETED
        }

        static void Main(string[] args) {
            //A classe TCPListener implementa os métodos da classe Socket utilizando o protócolo TCP, permitindo uma maior abstração das etapas tipicamente associadas ao Socket.
            TcpListener ServerSocket = new TcpListener(IPAddress.Any, 1337);
            Console.WriteLine($"Listening on: {((IPEndPoint)ServerSocket.LocalEndpoint).Address}:{((IPEndPoint)ServerSocket.LocalEndpoint).Port}");

            //A chamada ao método "Start" inicia o Socket para ficar à escuta de novas conexões por parte dos clientes
            ServerSocket.Start();
            Thread thread = new Thread(() => {
                Program.MainThread(ServerSocket);
            });
            thread.Start();
        }

        public static void MainThread(TcpListener ServerSocket) {
                //Ciclo infinito para ficar à espera que um cliente Socket/TCP até quando pretender conectar-se

                TcpClient client = ServerSocket.AcceptTcpClient();
                Thread thread = new Thread(() => {
                    Program.MainThread(ServerSocket);
                });
                thread.Start();
                //Só avança para esta parte do código, depois de um cliente ter se conectado ao servidor
                handle_client(client);
        }

        public static void handle_client(TcpClient client) {

            string id = Guid.NewGuid().ToString();
            Console.WriteLine($"{id} Connected!");
            Thread.CurrentThread.Name = id;
            // Neste método, é iniciada a gestão da comunicação do servidor com o cliente
            OnClientConnected(client);
            try {
                ParseFile(client);
            } catch (Exception e) {
                Console.WriteLine(e.Message);
            }
            CloseConnection(client);
            Console.WriteLine($"{id} Disconnected!");
        }


        public static void ParseFile(TcpClient client) {
            List<string> Erros = new List<string>();
            byte[] bRec = new byte[1024];
            int n;
            var sb = new StringBuilder();
            string filename = Guid.NewGuid().ToString();

            do {
                n = client.GetStream().Read(bRec, 0, bRec.Length);
                sb.Append(Encoding.UTF8.GetString(bRec, 0, n));
            } while (!Encoding.UTF8.GetString(bRec, 0, n).Contains("\0\0\0"));
            sb.Replace("\0\0\0", "");
            File.WriteAllText($"./Coberturas/{filename}.csv", sb.ToString(), Encoding.UTF8);
            var hash = ChecksumUtil.GetChecksum(HashingAlgoTypes.SHA256, $"./Coberturas/{filename}.csv");

            bRec = Encoding.UTF8.GetBytes($"{(int)StatusCode.OK} - File Received\0\0\0");
            Console.WriteLine($"{(int)StatusCode.OK} - File Received\0\0\0");
            client.GetStream().Write(bRec, 0, bRec.Length);
            Console.WriteLine($"{Thread.CurrentThread.Name} is requesting access");
            mutex.WaitOne();
            Console.WriteLine($"{Thread.CurrentThread.Name} is in the protected area");
            if (!dataContext.Ficheiros.Any(x => x.Hash == hash)) {

                Ficheiro file = new Ficheiro();
                file.Hash = hash;
                dataContext.Ficheiros.Add(file);
                dataContext.SaveChanges();
                mutex.ReleaseMutex();
                Console.WriteLine($"{Thread.CurrentThread.Name} released the mutex");
                var lista = CsvParser.CsvToList($"./Coberturas/{filename}.csv", ';');
                if (lista[0][0] != "Operador" || lista[0][1] != "Município" || lista[0][2] != "Rua" || lista[0][3] != "Número" || lista[0][4] != "Apartamento" || lista[0][5] != "Owner") {
                    Console.WriteLine($"{(int)StatusCode.ERROR} - {StatusCode.ERROR}: Invalid File!\0\0\0");
                    bRec = Encoding.UTF8.GetBytes($"{(int)StatusCode.ERROR} - {StatusCode.ERROR}: Invalid File!\0\0\0");
                    client.GetStream().Write(bRec, 0, bRec.Length);
                    File.Delete($"./Coberturas/{filename}.csv");
                } else {

                    //Lista de Coberturas que vão estar no Document
                    List<Cobertura> coberturas = new List<Cobertura>();

                    lista.RemoveAt(0);
                    lista = lista.OrderBy(x => x[1]).ToList();
                    lista.Insert(0, new List<string>() { "Operador", "Município", "Rua", "Número", "Apartamento", "Owner" });
                    Console.WriteLine($"{Thread.CurrentThread.Name} is requesting access");
                    mutex.WaitOne();
                    Console.WriteLine($"{Thread.CurrentThread.Name} is in the protected area");
                    dataContext.Logs.Add(new Logs() { DataInicio = DateTime.Now, Estado = FileStatus.OPEN.ToString(), Ficheiro = $"{filename}.csv", Operador = lista[1][0] });
                    dataContext.Logs.Add(new Logs() { DataInicio = DateTime.Now, Estado = FileStatus.IN_PROGRESS.ToString(), Ficheiro = $"{filename}.csv", Operador = lista[1][0] });
                    dataContext.SaveChanges();
                    mutex.ReleaseMutex();
                    Console.WriteLine($"{Thread.CurrentThread.Name} released the mutex");
                    foreach (var item in lista) {
                        if (item[0] != "Operador") {
                            if (String.IsNullOrEmpty(item[0]) || String.IsNullOrEmpty(item[1]) ||
                                String.IsNullOrEmpty(item[2]) || String.IsNullOrEmpty(item[3])) {

                                Console.WriteLine($"{(int)StatusCode.ERROR} - {StatusCode.ERROR}: Parsing error, empty or null value! '{item[0]};{item[1]};{item[2]};{item[3]};{item[4]};{item[5]} is invalid! File: {filename}.csv\0\0\0");
                                Erros.Add($"{(int)StatusCode.ERROR} - {StatusCode.ERROR}: Parsing error, empty or null value! '{item[0]};{item[1]};{item[2]};{item[3]};{item[4]};{item[5]} is invalid!\n");
                                Console.WriteLine($"{Thread.CurrentThread.Name} is requesting access");
                                mutex.WaitOne();
                                Console.WriteLine($"{Thread.CurrentThread.Name} is in the protected area");
                                dataContext.Logs.Add(new Logs() { DataInicio = DateTime.Now, Estado = FileStatus.ERROR.ToString(), Ficheiro = $"{filename}.csv", Operador = lista[1][0] });
                                dataContext.SaveChanges();
                                mutex.ReleaseMutex();
                                Console.WriteLine($"{Thread.CurrentThread.Name} released the mutex");
                            } else {
                                if (String.IsNullOrEmpty(item[5])) item[5] = "false";
                                else item[5] = "true";
                                Cobertura cobertura = new Cobertura();
                                cobertura.Operador = item[0];
                                cobertura.Municipio = item[1];
                                cobertura.Rua = item[2];
                                cobertura.Numero = item[3];
                                cobertura.Apartamento = item[4];
                                cobertura.Owner = Boolean.Parse(item[5]);

                                coberturas.Add(cobertura);
                            }
                        }
                    }
                    Console.WriteLine($"{Thread.CurrentThread.Name} is requesting access");
                    mutex.WaitOne();
                    Console.WriteLine($"{Thread.CurrentThread.Name} is in the protected area");
                    dataContext.Logs.Add(new Logs() { DataInicio = DateTime.Now, Estado = FileStatus.COMPLETED.ToString(), Ficheiro = $"{filename}.csv", Operador = lista[1][0] });
                    dataContext.SaveChanges();
                    mutex.ReleaseMutex();
                    Console.WriteLine($"{Thread.CurrentThread.Name} released the mutex");
                    GuardarCoberturasBaseDados(coberturas, client, Erros);
                    string errostobuff = "";
                    foreach (var e in Erros) {
                        errostobuff += e;
                    }
                    errostobuff += $"{(int)StatusCode.OK} - {StatusCode.OK}: File processed!\0\0\0";
                    bRec = Encoding.UTF8.GetBytes(errostobuff);
                    client.GetStream().Write(bRec, 0, bRec.Length);
                    Console.WriteLine($"{(int)StatusCode.OK} - {StatusCode.OK}: File processed!\0\0\0");
                }



            } else {
                mutex.ReleaseMutex();
                Console.WriteLine($"{Thread.CurrentThread.Name} released the mutex");
                Console.WriteLine($"{(int)StatusCode.ERROR} - {StatusCode.ERROR}: File already processed!\0\0\0");
                bRec = Encoding.UTF8.GetBytes(
                    $"{(int)StatusCode.ERROR} - {StatusCode.ERROR}: File already processed!\0\0\0");
                client.GetStream().Write(bRec, 0, bRec.Length);
                File.Delete($"./Coberturas/{filename}.csv");
                return;
            }
            Console.WriteLine($"Saved as '{filename}.csv'");
        }

        public static void GuardarCoberturasBaseDados(List<Cobertura> coberturas, TcpClient client, List<string> Erros) {
            byte[] bRec = new byte[1024];
            Console.WriteLine($"{Thread.CurrentThread.Name} is requesting access");
            mutex.WaitOne();
            Console.WriteLine($"{Thread.CurrentThread.Name} is in the protected area");
            foreach (var cobertura in coberturas) {
                //Verifico se não existe nenhuma cobertura com a mesma morada
                if (!dataContext.Coberturas.Any(c => c.Municipio == cobertura.Municipio && c.Rua == cobertura.Rua && c.Apartamento == cobertura.Apartamento && c.Numero == cobertura.Numero)) {
                    dataContext.Coberturas.Add(cobertura);

                } else {
                    Console.WriteLine($"{(int)StatusCode.ERROR} - {StatusCode.ERROR}: The cobertura : {cobertura.Municipio}, {cobertura.Rua}, {cobertura.Numero}, {cobertura.Apartamento}  wasn't accepted because it already exists!\0\0\0");
                    Erros.Add($"{(int)StatusCode.ERROR} - {StatusCode.ERROR}: The cobertura : {cobertura.Municipio}, {cobertura.Rua}, {cobertura.Numero}, {cobertura.Apartamento} wasn't accepted because it already exists!\n");

                }

            }
            dataContext.SaveChanges();
            mutex.ReleaseMutex();
            Console.WriteLine($"{Thread.CurrentThread.Name} released the mutex");
        }
        public static List<Cobertura> GetDataModelsMunicipio() {
            mutex.WaitOne();
            List<Cobertura> data = dataContext.Coberturas.OrderBy(x => x.Municipio).OrderBy(x => x.Rua).ToList();
            mutex.ReleaseMutex();
            return data;
        }

        public static List<Cobertura> GetDataModelMunicipio(string municipio) {
            mutex.WaitOne();
            List<Cobertura> data = dataContext.Coberturas.Where(x => x.Municipio.Contains(municipio)).OrderBy(x => x.Rua).ToList();
            mutex.ReleaseMutex();
            return data;
        }

        public static void OnClientConnected(TcpClient client) {
            var bytes = Encoding.UTF8.GetBytes($"{(int)StatusCode.OK} - {StatusCode.OK}\0\0\0");
            client.GetStream().Write(bytes, 0, bytes.Length);
        }

        public static void CloseConnection(TcpClient client) {
            byte[] buffer = new byte[1024];
            StringBuilder data = new StringBuilder();
            if (!client.Connected) return;
            do {
                int byte_count = client.GetStream().Read(buffer, 0, buffer.Length);
                data.Append(Encoding.UTF8.GetString(buffer, 0, byte_count));
            } while (!data.ToString().Contains("\0\0\0"));

            if (data.ToString().Contains("QUIT")) {
                buffer = Encoding.UTF8.GetBytes($"{(int)StatusCode.BYE} - {StatusCode.BYE}\0\0\0");
                client.GetStream().Write(buffer, 0, buffer.Length);
                client.Client.Shutdown(SocketShutdown.Both);
                client.Close();
            }
        }
    }
}
