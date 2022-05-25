using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ManagedBass;
using ManagedBass.Opus;
using ReactiveUI;

namespace LivePlayer.UI.Models;

public class CustomMediaPlayer : MediaPlayer
{
    private int _lastStream = 0;
    
    protected override int OnLoad(string fileName)
    {
        if (_lastStream != 0)
            Bass.StreamFree(_lastStream);
        
        if (!Uri.TryCreate(fileName, UriKind.Absolute, out var uri) || uri.Scheme == "file") 
            return 0;

        var create = Bass.CreateStream(fileName, 0, BassFlags.StreamDownloadBlocks, null, IntPtr.Zero);
        if (create == 0)
            throw new Exception(Bass.LastError.ToString());

        _lastStream = create;
        return create;

    }

    protected override void InitProperties()
    {
        base.InitProperties();
        Frequency = 0;  //48000;
        //Volume = 1.0;
        //Balance = 0;
        //Loop = false;
    }
}