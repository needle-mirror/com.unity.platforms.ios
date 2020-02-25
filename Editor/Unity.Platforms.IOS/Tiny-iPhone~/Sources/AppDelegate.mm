#import "AppDelegate.h"
#import "UnityTinyIOS.h"

@implementation AppDelegate

@synthesize m_window;
@synthesize m_view;
@synthesize m_viewController;

- (BOOL)application:(UIApplication *)application didFinishLaunchingWithOptions:(NSDictionary *)launchOptions {
    // setting path to read resources
    NSString *placeholderFile = @"placeholder";
    NSString *path = [[NSBundle mainBundle] pathForResource:placeholderFile ofType:@""];
    chdir([path substringToIndex: (path.length - placeholderFile.length)].UTF8String);
    
    startapp();
    
    CGRect rect = [ [UIScreen mainScreen] bounds];
    m_window = [ [UIWindow alloc] initWithFrame: rect];
    m_view = [ [TinyView alloc] initWithFrame: rect];
    
    m_viewController = [[TinyViewController alloc] init];
    m_viewController.view = m_view;
    
    [m_window setRootViewController:m_viewController];
    [m_window makeKeyAndVisible];
    
    float scaleFactor = [[UIScreen mainScreen] scale];
    [m_view setContentScaleFactor: scaleFactor];
    
    [[NSNotificationCenter defaultCenter] addObserver:self selector:@selector(deviceOrientationChanged:) name:UIDeviceOrientationDidChangeNotification object:[UIDevice currentDevice]];
    
    return YES;
}


- (void)applicationWillResignActive:(UIApplication *)application {
    [m_view stop];
    pauseapp(1);
}


- (void)applicationDidEnterBackground:(UIApplication *)application {
}


- (void)applicationWillEnterForeground:(UIApplication *)application {
}


- (void)applicationDidBecomeActive:(UIApplication *)application {
    pauseapp(0);
    [m_view start];
}


- (void)applicationWillTerminate:(UIApplication *)application {
    destroyapp();
}

- (void)deviceOrientationChanged:(NSNotification *)note
{
    deviceOrientationChanged((uint32_t)[[note object] orientation]);
}

@end

void rotateToDeviceOrientation()
{
    [UIViewController attemptRotationToDeviceOrientation];
}

void rotateToAllowedOrientation()
{
    TinyViewController *viewController = ((AppDelegate*)[[UIApplication sharedApplication] delegate]).m_viewController;
    UIViewController *dummy = [[UIViewController alloc] init];
    dummy.view = [[UIView alloc] init];
    [viewController presentViewController:dummy animated:NO completion:^{
        [viewController dismissViewControllerAnimated:YES completion:nil];
    }];
}

// TODO: investigate if this method can be implemented properly
// current variant uses undocumented device orientation access and causes warnings: 
// [App] if we're in the real pre-commit handler we can't actually add any new fences due to CA restriction
/*static int orientationToSkip = -1;
void rotateToOrientation(int orientation)
{
    int deviceOrientation = (int)[UIDevice currentDevice].orientation;
    orientationToSkip = orientation;
    [[UIDevice currentDevice] setValue: [NSNumber numberWithInt: orientation] forKey:@"orientation"];
    [UIViewController attemptRotationToDeviceOrientation];
    orientationToSkip = deviceOrientation;
    [[UIDevice currentDevice] setValue: [NSNumber numberWithInt: deviceOrientation] forKey:@"orientation"];
}*/
