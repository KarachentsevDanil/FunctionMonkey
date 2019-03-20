using FunctionMonkey.Abstractions.Builders.Model;

namespace FunctionMonkey.Model.OutputBindings
{
    public class ServiceBusQueueOutputBinding : AbstractConnectionStringOutputBinding
    {
        public string QueueName { get; set; }

        public override bool IsReturnType => true;
    }
}