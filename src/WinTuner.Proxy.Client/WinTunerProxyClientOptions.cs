using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Svrooij.WinTuner.Proxy.Client;
public class WinTunerProxyClientOptions
{
    public Uri? BaseAddress { get; set; }
    public string Code { get; set; } = default!;
}
