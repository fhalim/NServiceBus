﻿using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace NServiceBus.Management.Retries.Tests
{
    using Helpers;

    [TestFixture]
    public class DefaultRetryPolicyTests
    {
        private readonly int[] _expectedResults = new[] {10,20,30};
        private TransportMessage _message;

        [SetUp]
        public void SetUp()
        {
            _message = new TransportMessage {Headers = new Dictionary<string, string>()};
        }

        [Test]
        public void The_time_span_should_increase_with_10_sec_for_every_retry()
        {            
            for (int i=0; i<3; i++)
            {                
                var timeSpan = DefaultRetryPolicy.Validate(_message);
                
                Defer();

                Assert.AreEqual(_expectedResults[i], timeSpan.Seconds);
            }          
        }

        [Test]
        public void The_default_time_out_should_be_1_day()
        {
            TransportMessageHelpers.SetHeader(_message, SecondLevelRetriesHeaders.RetriesTimestamp, DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow.AddDays(-1).AddSeconds(-1)));
            var hasTimedOut = DefaultRetryPolicy.HasTimedOut(_message);
            Assert.IsTrue(hasTimedOut);
        }

        private void Defer()
        {
            TransportMessageHelpers.SetHeader(_message, Headers.Retries, (TransportMessageHelpers.GetNumberOfRetries(_message) + 1).ToString());
        }
    }
}