using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace Svrooij.WinTuner.CmdLets.Commands;
internal static class CmdLetExtensions
{
    /// <summary>
    /// Extension method to write a collection of objects to the output stream of a Cmdlet.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="cmdlet"></param>
    /// <param name="collection"></param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <remarks>Because <see cref="Cmdlet.WriteObject(object, bool)"/> does not always work correctly</remarks>
    internal static void WriteCollection<T>(this PSCmdlet cmdlet, IEnumerable<T> collection)
    {
        if (cmdlet is null)
        {
            throw new ArgumentNullException(nameof(cmdlet));
        }
        if (collection is null)
        {
            throw new ArgumentNullException(nameof(collection));
        }
        // Write the collection to the output stream
        foreach (var item in collection)
        {
            cmdlet.WriteObject(item);
        }
    }
}
