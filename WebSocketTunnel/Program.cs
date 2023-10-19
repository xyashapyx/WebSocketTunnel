// See https://aka.ms/new-console-template for more information

using System.Text;

Console.WriteLine("Hello, World!");

var message = "192.168.111.13:4007";

byte[] bytes = Encoding.ASCII.GetBytes(message);

Memory<byte> dd = bytes; 

var cc = (dd[..3]).ToArray();

var arr = new byte[128];
var bigBytes = new Span<byte>(arr);
bytes.CopyTo(bigBytes);
var message1 = Encoding.ASCII.GetString(bigBytes);
Console.WriteLine(bytes);