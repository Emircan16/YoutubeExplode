﻿// ------------------------------------------------------------------ 
//  Solution: <YoutubeExplode>
//  Project: <YoutubeExplodeDemo>
//  File: <MainViewModel.cs>
//  Created By: Alexey Golub
//  Date: 08/08/2016
// ------------------------------------------------------------------ 

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using Microsoft.Win32;
using Tyrrrz.Extensions;
using YoutubeExplode;
using YoutubeExplode.Models;

namespace YoutubeExplodeDemo.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly YoutubeClient _client;

        private string _videoID;

        public string VideoID
        {
            get { return _videoID; }
            set { Set(ref _videoID, value); }
        }

        private VideoInfo _videoInfo;

        public VideoInfo VideoInfo
        {
            get { return _videoInfo; }
            private set
            {
                Set(ref _videoInfo, value);
                RaisePropertyChanged(() => VideoInfoVisible);
            }
        }

        public bool VideoInfoVisible => VideoInfo != null;

        private double _downloadProgress;

        public double DownloadProgress
        {
            get { return _downloadProgress; }
            set { Set(ref _downloadProgress, value); }
        }

        private bool _isDownloading;

        public bool IsDownloading
        {
            get { return _isDownloading; }
            set
            {
                Set(ref _isDownloading, value);
                DownloadVideoCommand.RaiseCanExecuteChanged();
            }
        }

        // Commands
        public RelayCommand GetDataCommand { get; }
        public RelayCommand<VideoStreamEndpoint> OpenVideoCommand { get; }
        public RelayCommand<VideoStreamEndpoint> DownloadVideoCommand { get; }

        public MainViewModel()
        {
            _client = new YoutubeClient();

            // Commands
            GetDataCommand = new RelayCommand(GetDataAsync);
            OpenVideoCommand = new RelayCommand<VideoStreamEndpoint>(vse => Process.Start(vse.Url));
            DownloadVideoCommand = new RelayCommand<VideoStreamEndpoint>(DownloadVideoAsync, vse => !IsDownloading);
        }

        private async void GetDataAsync()
        {
            // Check params
            if (VideoID.IsBlank())
                return;

            // Reset data
            VideoInfo = null;

            // Parse URL if necessary
            string id;
            if (!_client.TryParseVideoID(VideoID, out id))
                id = VideoID;

            // Perform the request
            await Task.Run(() =>
            {
                try
                {
                    VideoInfo = _client.GetVideoInfo(id);
                }
                catch (Exception ex)
                {
                    Dialogs.Error(ex.Message);
                }
            });
        }

        private async void DownloadVideoAsync(VideoStreamEndpoint videoStreamEndpoint)
        {
            // Check params
            if (videoStreamEndpoint == null) return;
            if (VideoInfo == null) return;

            // Copy values
            string title = VideoInfo.Title;
            string ext = videoStreamEndpoint.FileExtension;

            // Select destination
            var sfd = new SaveFileDialog
            {
                AddExtension = true,
                DefaultExt = ext,
                FileName = $"{title}.{ext}".Except(Path.GetInvalidFileNameChars()),
                Filter = $"{ext.ToUpperInvariant()} Video Files|*.{ext}|All files|*.*"
            };
            if (sfd.ShowDialog() == false) return;
            string filePath = sfd.FileName;

            // Try download
            IsDownloading = true;
            bool success = await Task.Run(() =>
            {
                using (var output = File.Create(filePath))
                using (var input = _client.DownloadVideo(videoStreamEndpoint))
                {
                    if (input == null) return false;

                    // Read the response and copy it to output stream
                    var buffer = new byte[1024];
                    int bytesRead;
                    do
                    {
                        bytesRead = input.Read(buffer, 0, 1024);
                        output.Write(buffer, 0, bytesRead);
                        DownloadProgress += 1.0*bytesRead/videoStreamEndpoint.FileSize;
                    } while (bytesRead > 0);
                }
                return true;
            });
            IsDownloading = false;

            // Finalize
            if (success)
            {
                if (Dialogs.PromptYesNo(
                    $"Video ({title}) has been downloaded!{Environment.NewLine}Do you want to open it?"))
                    Process.Start(filePath);
            }
            else
            {
                Dialogs.Error("Could not download the video");
            }
        }
    }
}