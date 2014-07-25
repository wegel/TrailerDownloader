using Ninject;
using TrailerDownloader.Searchers;

namespace TrailerDownloader
{
    public class Bootstrapper
    {
        public static void Bootstrap()
        {
            Kernel = new StandardKernel();
            Kernel.Bind<IGoogleSearch>().To<GoogleSearchScraper>();
        }

        public static IKernel Kernel { get; set; }
    }
}


 
