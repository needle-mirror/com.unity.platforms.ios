#import "TinyViewController.h"
#import "UnityTinyIOS.h"
#import "iPhoneInputImpl.h"
#import <QuartzCore/CAEAGLLayer.h>

#if __IPHONE_8_0 && !TARGET_IPHONE_SIMULATOR
#import <Metal/Metal.h>
#import <QuartzCore/CAMetalLayer.h>
#define HAS_METAL_SDK
#endif

#ifdef HAS_METAL_SDK
static id<MTLDevice> m_device = NULL;
#else
static void* m_device = NULL;
#endif

static void* m_nwh = NULL;

static UIInterfaceOrientationMask m_orientationMask;

static bool appStarted = false;

extern bool waitForManagedDebugger;

@implementation TinyView

+ (Class)layerClass
{
#ifdef HAS_METAL_SDK
    Class metalClass = NSClassFromString(@"CAMetalLayer");    // is metal runtime sdk available
    if (metalClass != nil)
    {
        m_device = MTLCreateSystemDefaultDevice(); // is metal supported on this device (is there a better way to do this - without creating device ?)
        if (m_device)
        {
            // TODO: get rid of OpenGLES for iOS when problem with Metal on A7/A8 based devices is fixed
            if ([m_device supportsFeatureSet:MTLFeatureSet_iOS_GPUFamily3_v1])
                return metalClass;
        }
    }
#endif

    return [CAEAGLLayer class];
}

- (id)initWithFrame:(CGRect)rect
{
    self = [super initWithFrame:rect];
    if (nil != self)
    {
        m_nwh = (__bridge void*)self.layer;
        m_orientationMask = [[UIDevice currentDevice] userInterfaceIdiom] == UIUserInterfaceIdiomPhone ? UIInterfaceOrientationMaskAllButUpsideDown :  UIInterfaceOrientationMaskAll;
        // do we need to pass m_device to PlatformData.context ?
        InputInit(self);
    }
    return self;
}

- (void)layoutSubviews
{
    uint32_t frameW = (uint32_t)(self.contentScaleFactor * self.frame.size.width);
    uint32_t frameH = (uint32_t)(self.contentScaleFactor * self.frame.size.height);
    uint32_t orientation = 0;
    if (@available(iOS 13, *))
    {
        orientation = (uint32_t)[(UIWindowScene*)[[UIApplication sharedApplication] connectedScenes].allObjects.firstObject interfaceOrientation];
    }
    else
    {
        orientation = (uint32_t)[[UIApplication sharedApplication] statusBarOrientation];
    }

    CancelTouches();
    init(m_nwh, frameW, frameH, orientation);
}

- (void)start
{
    if (nil == m_displayLink)
    {
        m_displayLink = [self.window.screen displayLinkWithTarget:self selector:@selector(renderFrame)];
        [m_displayLink addToRunLoop:[NSRunLoop currentRunLoop] forMode:NSRunLoopCommonModes]; // or NSDefaultRunLoopMode ?
    }
}

- (void)stop
{
    if (nil != m_displayLink)
    {
        [m_displayLink invalidate];
        m_displayLink = nil;
    }
}

- (void)renderFrame
{
    InputProcess();
    if (!waitForManagedDebugger)
        step();
}
@end

@interface TinyViewController()
@end

@implementation TinyViewController

- (void)viewDidLoad
{
    [super viewDidLoad];
}

- (void) viewDidAppear:(BOOL) animated
{
    [super viewDidAppear:animated];
    if (!appStarted)
    {
        startapp();
        appStarted = true;
    }
}

- (BOOL)shouldAutorotate
{
    return YES;
}

- (UIInterfaceOrientationMask)supportedInterfaceOrientations
{
    return m_orientationMask;
}

- (void)viewWillTransitionToSize:(CGSize)size withTransitionCoordinator:(id<UIViewControllerTransitionCoordinator>)coordinator
{
    [coordinator animateAlongsideTransition: nil completion:^(id<UIViewControllerTransitionCoordinatorContext> context)
    {
        [self.view setNeedsLayout];
    }];
    [super viewWillTransitionToSize: size withTransitionCoordinator: coordinator];
}

- (BOOL)prefersStatusBarHidden {
    return YES;
}

- (void)touchesBegan:(NSSet<UITouch *> *)touches withEvent:(UIEvent *)event
{
    ProcessTouchEvents(self.view, touches, [event allTouches]);
}

- (void)touchesMoved:(NSSet<UITouch *> *)touches withEvent:(UIEvent *)event
{
    ProcessTouchEvents(self.view, touches, [event allTouches]);
}

- (void)touchesEnded:(NSSet<UITouch *> *)touches withEvent:(UIEvent *)event
{
    ProcessTouchEvents(self.view, touches, [event allTouches]);
}

- (void)touchesCancelled:(NSSet<UITouch *> *)touches withEvent:(UIEvent *)event
{
    ProcessTouchEvents(self.view, touches, [event allTouches]);
}

@end

void setOrientationMask(int orientation)
{
    m_orientationMask = (UIInterfaceOrientationMask)orientation;
}


