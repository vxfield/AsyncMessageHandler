using AsyncMessageHandler;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace UnitTestExample
{
    public class MessageHandlerTest
    {
        private class TestMessage : Message 
        {
            public int TestId;
            public int TestDelay;
            public TestMessage(int id, int delay)
            {
                TestId = id;
                TestDelay = delay;
            }
        }
        private class BadMessage : TestMessage
        {
            public BadMessage(int id, int delay) : base(id, delay)
            {
            }
        }
        private class BadMessageException : Exception { }

        [Fact]
        public void TestUnordered()
        {
            var tasks = CreateAndWaitMessages(MessageHandlingMode.Unordered, out var completionOrder);

            //We should have 4 completed tasks, since #4 should have failed
            Assert.Equal(4, completionOrder.Count);

            //Task #4 should have failed
            Assert.IsType<BadMessageException>(tasks[3].Exception.InnerException);

            //The order of completion should be 2, 5, 3, 1
            Assert.Equal(2, completionOrder[0].TestId);
            Assert.Equal(5, completionOrder[1].TestId);
            Assert.Equal(3, completionOrder[2].TestId);
            Assert.Equal(1, completionOrder[3].TestId);
        }

        [Fact]
        public void TestOrderedAbortAllOnAnyFailure()
        {
            var tasks = CreateAndWaitMessages(MessageHandlingMode.OrderedAbortAllOnAnyFailure, out var completionOrder);

            //We should have 3 completed tasks, since #4 should have failed 
            //and #5 should not execute
            Assert.Equal(3, completionOrder.Count);

            //Task #4 should have failed
            Assert.IsType<BadMessageException>(tasks[3].Exception?.GetBaseException());

            //The order of completion should be 1, 2, 3
            Assert.Equal(1, completionOrder[0].TestId);
            Assert.Equal(2, completionOrder[1].TestId);
            Assert.Equal(3, completionOrder[2].TestId);
        }

        [Fact]
        public void TestOrderedContinueOnAnyFailure()
        {
            var tasks = CreateAndWaitMessages(MessageHandlingMode.OrderedContinueOnAnyFailure, out var completionOrder);

            //We should have 4 completed tasks, since #4 should have failed 
            Assert.Equal(4, completionOrder.Count);

            //Task #4 should have failed
            Assert.IsType<BadMessageException>(tasks[3].Exception?.GetBaseException());

            //The order of completion should be 1, 2, 3
            Assert.Equal(1, completionOrder[0].TestId);
            Assert.Equal(2, completionOrder[1].TestId);
            Assert.Equal(3, completionOrder[2].TestId);
            Assert.Equal(5, completionOrder[3].TestId);
        }

        private static List<Task> CreateAndWaitMessages(MessageHandlingMode mode, out List<TestMessage> completionOrder)
        {
            completionOrder = new List<TestMessage>();
            var completionOrder2 = completionOrder;
            var messageHandler = new MessageHandler(
                mode,
                async (message) =>
                {
                    if (message is BadMessage)
                    {
                        throw new BadMessageException();
                    }
                    if (message is TestMessage testMessage)
                    {
                        await Task.Delay(testMessage.TestDelay);
                        completionOrder2.Add(testMessage);
                    }
                });
            var tasks = new List<Task>() {
                messageHandler.HandleAsync(new TestMessage(1, 3000)),
                messageHandler.HandleAsync(new TestMessage(2, 0)),
                messageHandler.HandleAsync(new TestMessage(3, 2000)),
                messageHandler.HandleAsync(new BadMessage(4, 0)),
                messageHandler.HandleAsync(new TestMessage(5, 1000)),
            };

            //just wait for the tasks but don't throw exceptions
            foreach (var task in tasks)
            {
                try
                {
                    Task.WaitAll(task);
                }
                catch { }
            }

            return tasks;
        }
    }
}
