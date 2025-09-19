using FluentFTP;
using FluentFTP.Helpers;
using FluentFTP.Streams;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Net;
using System.Runtime.CompilerServices;

namespace phizapi.Services
{
    public class FtpService
    {
        private readonly IConfiguration _config;
        private readonly AsyncFtpClient _client;
        public FtpService(IConfiguration config)
        {
            _config = config;

            _client = new AsyncFtpClient(
              config["Ftp:Host"],
              config["Ftp:User"],
              config["Ftp:Pass"],
              21
           );

        }



        public async Task DeleteFileAsync(string remotePath)
        {
            await DoFtpAction<object>(async() => {
                await _client.DeleteFile(remotePath);
                return default;
            });
        }
        public async Task CreateFileAsync(IFormFile file, string remotePath)
        {
            using var stream = file.OpenReadStream();
            await DoFtpAction<object>(async () =>
            {
                using var stream = file.OpenReadStream();
                await _client.UploadStream(stream, remotePath, FtpRemoteExists.Overwrite, true);
                return default;
            });
          
        }
        public async Task<byte[]> GetBytes(string remotePath)
        {

            return await DoFtpAction(async () =>
            {
                if (await _client.FileExists(remotePath))
                {
                    return await _client.DownloadBytes(remotePath, 0);
                }
                else
                {
                    return new byte[0];
                }
           
            });

        }
        private async Task<T> DoFtpAction<T>(Func<Task<T>> action)
        {
            await _client.Connect();
            var res = await action.Invoke();
            await _client.Disconnect();

            return res;
        }



    }
}
