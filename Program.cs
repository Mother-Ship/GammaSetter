using Newtonsoft.Json.Linq;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Xml.Linq;

class Program
{
    // Import the necessary GDI32 functions to adjust the Gamma
    [DllImport("gdi32.dll", EntryPoint = "SetDeviceGammaRamp")]
    private static extern bool SetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

    [DllImport("user32.dll", EntryPoint = "GetDC")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "ReleaseDC")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    // Struct for gamma ramp
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct RAMP
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Red;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Green;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Blue;
    }

    static void SetGamma(double gammaValue)
    {
    
        RAMP ramp = new RAMP();
        ramp.Red = new ushort[256];
        ramp.Green = new ushort[256];
        ramp.Blue = new ushort[256];

        for (int i = 1; i < 256; i++)
        {   // 0.3-3.3 越小越亮
       
            int value = (int)(Math.Pow((i + 1) / 256.0, gammaValue) * 65535 + 0.5);
            if (value > 65535) value = 65535;
            ramp.Red[i] = ramp.Green[i] = ramp.Blue[i] = (ushort)value;
        }

        IntPtr hDC = GetDC(IntPtr.Zero);
        SetDeviceGammaRamp(hDC, ref ramp);
        ReleaseDC(IntPtr.Zero, hDC);
    }

    static async Task Main(string[] args)
    {
   
   
        using (ClientWebSocket webSocket = new ClientWebSocket())
        {            Uri serverUri = new Uri("ws://localhost:24050/ws");
            await webSocket.ConnectAsync(serverUri, CancellationToken.None);

            byte[] buffer = new byte[1024 * 4];
            StringBuilder completeMessage = new StringBuilder();
            ArraySegment<byte> segment = new ArraySegment<byte>(buffer);

            while (webSocket.State == WebSocketState.Open)
            {
                double gamma = 1;

                WebSocketReceiveResult result;
                do
                {
                    // 读取WebSocket消息
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    // 将接收到的消息片段拼接成完整的消息
                    completeMessage.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage); // 确保接收到的是完整的消息
                string message = completeMessage.ToString();
                completeMessage.Clear();
                 // Parse the JSON to get menu.bm.stats.AR
                 JObject jsonMessage = JObject.Parse(message);
                float arValue = jsonMessage["menu"]?["bm"]?["stats"]?["AR"]?.Value<float>() ?? 0;

                arValue = Math.Min(Math.Max(arValue, 8f), 11f);

                var tempGamma = MapValue(arValue);
                if(tempGamma != gamma)
                {
                    gamma = tempGamma;
                    SetGamma(gamma);
                    Console.WriteLine($"Received AR value: {arValue}. Adjusted gamma to: {gamma}");
                }
               

            
            }
        }
    }
    static double MapValue(double input)
    {
        double output;

        if (input == 9)
        {
            // 如果输入是9，返回1
            output = 1;
        }
        else if (input < 9)
        {
            // 如果输入小于9，每减少0.1，输出增大0.1
            output = 1 + (9 - input) * 1.0;
        }
        else
        {
            // 如果输入大于9，每增加1，输出减少0.3，但最小不能小于0.3
            output = 1 - (input - 9) * 0.33;
            if (output < 0.3)
            {
                output = 0.3;
            }
        }

        return output;
    }
}
