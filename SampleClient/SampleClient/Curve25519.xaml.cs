using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace SampleClient
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Curve25519 : Page
    {
        private curve25519.Curve25519Native _native;
        private curve25519.Curve25519Native Native
        {
            get
            {
                if(_native == null)
                {
                    _native = new curve25519.Curve25519Native();
                }
                return _native;
            }
        }

        public Curve25519()
        {
            this.InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DateTime before = DateTime.UtcNow;

            byte [] privKey = Native.generatePrivateKey();
            byte[] pubKey = Native.generatePublicKey(privKey);

            DateTime after = DateTime.UtcNow;
            TimeSpan difference = after.Subtract(before);

            txtBlock.Text = string.Format("Private key: {0}, Public key: {1}, Total milliseconds: {2}", Convert.ToBase64String(privKey), Convert.ToBase64String(pubKey), difference.TotalMilliseconds);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            this.Frame.GoBack();
        }
    }
}
