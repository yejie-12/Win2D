// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT License. See LICENSE.txt in the project root for license information.

using ExampleGallery.BackgroundTask;
using System;
using System.Linq;
using Windows.ApplicationModel.Background;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace ExampleGallery
{
    public sealed partial class BackgroundTaskExample : UserControl
    {
        LiveTileUpdater liveTileUpdater = new LiveTileUpdater();

        bool isBusy = false;

        public BackgroundTaskExample()
        {
            this.InitializeComponent();

            liveTileUpdater.NewLiveTileGenerated += liveTileUpdater_NewLiveTileGenerated;
            liveTileUpdater.BackgroundTaskCompleted += liveTileUpdater_BackgroundTaskCompleted;
        }

        void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateUI();
        }

        void UpdateUI()
        {
            if (isBusy)
                return;

            if (liveTileUpdater.IsStarted)
            {
                VisualStateManager.GoToState(this, "ReadyToStop", true);
            }
            else
            {
                VisualStateManager.GoToState(this, "ReadyToStart", true);
            }

            var tileImages = liveTileUpdater.MostRecentTileImages;

            if (tileImages != null && tileImages.Length > 0)
            {
                ResultText.Text = string.Format("{0} tile images were generated:", tileImages.Length);

                ResultImages.ItemsSource = tileImages.Select((filename) => new { Name = filename, Image = new BitmapImage(new Uri("ms-appdata:///local/" + filename)) });
            }
            else
            {
                ResultText.Text = "Tile has not been generated yet.";
                ResultImages.ItemsSource = new object[0];
            }            
        }

        void DispatchUpdateUI()
        {
            var t = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                UpdateUI();
            });
        }

        static bool HasBackgroundExecutionAccess()
        {
            switch (BackgroundExecutionManager.GetAccessStatus())
            {
                case BackgroundAccessStatus.Unspecified:
                case BackgroundAccessStatus.DeniedByUser:

                // Denied was marked deprecated in 14393, but we must still handle it in case we are run on an older OS.
                // Therefore we temporarily disable the deprecated API warning.
#pragma warning disable 618
                case BackgroundAccessStatus.Denied:
#pragma warning restore 618
                    return false;

                default:
                    return true;
            }
        }

        void liveTileUpdater_BackgroundTaskCompleted(BackgroundTaskRegistration sender, BackgroundTaskCompletedEventArgs args)
        {
            DispatchUpdateUI();
        }

        void liveTileUpdater_NewLiveTileGenerated(LiveTileUpdater sender, object args)
        {
            DispatchUpdateUI();
        }

        async void StartButton_Click(object sender, RoutedEventArgs args)
        {
            isBusy = true;

            try
            {
                if (!HasBackgroundExecutionAccess())
                {
                    VisualStateManager.GoToState(this, "CheckingAccess", true);

                    await BackgroundExecutionManager.RequestAccessAsync();

                    if (!HasBackgroundExecutionAccess())
                    {
                        var dialog = new MessageDialog("This application does not have permission to schedule background tasks.  " +
                            "To enable, please go to the settings pane and choose Permissions and enable the Lock Screen permission.");
                        await dialog.ShowAsync();

                        VisualStateManager.GoToState(this, "ReadyToStart", true);
                        return;
                    }
                }
            }
            finally
            {
                isBusy = false;
            }

            liveTileUpdater.IsStarted = true;
            UpdateUI();
        }

        void StopButton_Click(object sender, RoutedEventArgs args)
        {
            liveTileUpdater.IsStarted = false;
            UpdateUI();
        }

        void ResetLiveTileButton_Click(object sender, RoutedEventArgs e)
        {
            var updater = TileUpdateManager.CreateTileUpdaterForApplication();
            updater.Clear();
        }
    }
}
