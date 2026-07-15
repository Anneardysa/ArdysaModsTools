/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ArdysaModsTools.Tests.Helpers
{
    public sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _responder;

        public List<HttpRequestMessage> Requests { get; } = new();

        public int CallCount { get; private set; }

        public FakeHttpMessageHandler(Func<HttpRequestMessage, int, HttpResponseMessage> responder)
        {
            _responder = responder ?? throw new ArgumentNullException(nameof(responder));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var response = _responder(request, CallCount);
            CallCount++;
            return Task.FromResult(response);
        }
    }
}
