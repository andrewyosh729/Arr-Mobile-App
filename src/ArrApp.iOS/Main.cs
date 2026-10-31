using ArrApp.UI;
using UIKit;

namespace ArrApp.iOS;

public class Application {
    static void Main(string[] args) {
        PlatformServices.ApiKeyProtector = new KeychainApiKeyProtector();
        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}
