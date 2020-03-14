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

-(void)reInitViewController
{
    UIViewController* prevController = m_viewController;
    m_viewController.view = nil;
    m_viewController = [[TinyViewController alloc] init];
    m_viewController.view = m_view;
    
    [UIView transitionWithView:self.m_window duration:0.15 options:UIViewAnimationOptionTransitionNone animations:^{
            [self.m_window setRootViewController:self.m_viewController];
            [self.m_window makeKeyAndVisible];
    } completion:^(BOOL){
        [prevController dismissViewControllerAnimated:NO completion:nil];
        [self.m_view setNeedsLayout];
    }];
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
    [((AppDelegate*)[[UIApplication sharedApplication] delegate]) reInitViewController];
}
