namespace MDTadusMod
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            MainPage = new MainPage();

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                throw (Exception)e.ExceptionObject;
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                throw e.Exception;
            };
        }



    }
}
