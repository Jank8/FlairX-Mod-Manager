using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Storage.Streams;

namespace FlairX_Mod_Manager
{
    /// <summary>
    /// MainWindow partial class - UI management, menu generation, and utility functions
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private readonly SemaphoreSlim _menuGenerationLock = new SemaphoreSlim(1, 1);
        private volatile bool _suppressMenuRegeneration = false;
    }
}