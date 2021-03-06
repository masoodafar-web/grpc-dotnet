﻿#region Copyright notice and license

// Copyright 2019 The gRPC Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;

namespace BenchmarkClient.ChannelFactory
{
    public class GrpcNetClientChannelFactory : IChannelFactory
    {
        private readonly string _target;
        private readonly bool _useTls;
        private readonly bool _useClientCertificate;
        private readonly GrpcWebMode? _useGrpcWeb;
        private readonly Dictionary<int, GrpcChannel> _channels;

        public GrpcNetClientChannelFactory(string target, bool useTls, bool useClientCertificate, GrpcWebMode? useGrpcWeb)
        {
            _target = target;
            _useTls = useTls;
            _useClientCertificate = useClientCertificate;
            _useGrpcWeb = useGrpcWeb;
            _channels = new Dictionary<int, GrpcChannel>();
        }

        public Task<ChannelBase> CreateAsync(int id)
        {
            if (_channels.TryGetValue(id, out var channel))
            {
                return Task.FromResult<ChannelBase>(channel);
            }

            var url = _useTls ? "https://" : "http://";
            url += _target;

            var httpClientHandler = new HttpClientHandler();
            httpClientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            if (_useClientCertificate)
            {
                var basePath = Path.GetDirectoryName(typeof(Program).Assembly.Location);
                var certPath = Path.Combine(basePath!, "Certs", "client.pfx");
                var clientCertificate = new X509Certificate2(certPath, "1111");
                httpClientHandler.ClientCertificates.Add(clientCertificate);
            }

            HttpMessageHandler httpMessageHandler = httpClientHandler;
            if (_useGrpcWeb != null)
            {
                httpMessageHandler = new GrpcWebHandler(_useGrpcWeb.Value, httpMessageHandler)
                {
                    HttpVersion = HttpVersion.Version11
                };
            }

            channel = GrpcChannel.ForAddress(url, new GrpcChannelOptions
            {
                HttpClient = new HttpClient(httpMessageHandler)
            });

            _channels[id] = channel;

            return Task.FromResult<ChannelBase>(channel);
        }

        public Task DisposeAsync(ChannelBase channel)
        {
            ((GrpcChannel)channel).Dispose();
            return Task.CompletedTask;
        }
    }
}
