using System.Threading.Tasks;
using Pulumi;

class Program
{
    async static Task<int> Main()
    {
        return await Deployment.RunAsync<MyStack>();
    }
}
