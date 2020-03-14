using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AsyncMessageHandler
{
    class Program
    {
        
        static void Main(string[] args)
        {
            TestMessageHandler(MessageHandlingMode.Unordered);
            TestMessageHandler(MessageHandlingMode.OrderedAbortAllOnAnyFailure);
            TestMessageHandler(MessageHandlingMode.OrderedContinueOnAnyFailure);
            Console.WriteLine();
            Console.WriteLine("Press enter to close...");
            Console.ReadLine();
        }

        private static void TestMessageHandler(MessageHandlingMode handlingMode)
        {
            Console.WriteLine($"===================================================");
            Console.WriteLine($"Testing handling mode '{handlingMode}'");
            Console.WriteLine($"===================================================");
            Console.WriteLine();

            try
            {
                var messageHandler = new MessageHandler(handlingMode, MyHandlerMethod);
                var tasks = new List<Task>();
                for (int i = 0; i < 10; i++)
                {
                    tasks.Add(
                        messageHandler.HandleAsync(new Message() { MessageText = $"Message {i}" })
                    );
                }

                Task.WaitAll(tasks.ToArray());
            }
            catch (Exception exception)
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine($"Uncaught exception: {exception.Message}");
                Console.WriteLine();
                Console.WriteLine();
            }
            Console.WriteLine();
            Console.WriteLine();
        }

        private static async Task MyHandlerMethod(Message message)
        {
            Console.WriteLine($"Processing message '{message.MessageText}'...");
            await Task.Delay(500 * (new Random()).Next(1, 5));
            if (message.MessageText.Contains("7")) throw new Exception("Bad number 7!");
            Console.WriteLine($"Message '{message.MessageText}' processed!");
        }
    }

    public class Message
    {
        public string MessageText { get; set; }
    }

    public interface IMessageHandler
    {
        Task HandleAsync(Message message);
    }

    public enum MessageHandlingMode
    {
        Unordered,
        OrderedAbortAllOnAnyFailure,
        OrderedContinueOnAnyFailure,
    }

    public class MessageHandler : IMessageHandler
    {
        public MessageHandler(MessageHandlingMode handlingMode, Func<Message, Task> handlerMethod)
        {
            HandlingMode = handlingMode;
            HandlerMethod = handlerMethod;
        }

        private Task currentTask = Task.CompletedTask;
        private readonly MessageHandlingMode HandlingMode;
        private readonly Func<Message, Task> HandlerMethod;

        public Task HandleAsync(Message message)
        {
            if (HandlingMode == MessageHandlingMode.Unordered)
            {
                return Task.Run(()=>HandlerMethod?.Invoke(message));
            }

            currentTask = new Task((state) => {
                var previousTask = (Task)state;
                try
                {
                    Task.WaitAll(previousTask);
                }
                catch (Exception) 
                {
                    if (HandlingMode == MessageHandlingMode.OrderedAbortAllOnAnyFailure) throw;
                    //log or trigger an OnException event (to be implemented)
                }
                Task.WaitAll(HandlerMethod?.Invoke(message));
            }, currentTask);
            currentTask.Start();
            return currentTask;
        }
    }
}
