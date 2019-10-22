#import "TinyViewController.h"
#import "UnityTinyIOS.h"
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
        // do we need to pass m_device to PlatformData.context ?
    }
    return self;
}

- (void)layoutSubviews
{
    uint32_t frameW = (uint32_t)(self.contentScaleFactor * self.frame.size.width);
    uint32_t frameH = (uint32_t)(self.contentScaleFactor * self.frame.size.height);
    init(m_nwh, frameW, frameH);
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

- (BOOL)shouldAutorotate
{
    return YES;
}

- (UIInterfaceOrientationMask)supportedInterfaceOrientations
{
    return [[UIDevice currentDevice] userInterfaceIdiom] == UIUserInterfaceIdiomPhone ? UIInterfaceOrientationMaskAllButUpsideDown :  UIInterfaceOrientationMaskAll;
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

// very simple implementation, no multi-touch for now
- (void)touchesBegan:(NSSet<UITouch *> *)touches withEvent:(UIEvent *)event
{
    touchevent(0, 0, [self touchX:touches.anyObject], [self touchY:touches.anyObject]);
}

- (void)touchesMoved:(NSSet<UITouch *> *)touches withEvent:(UIEvent *)event
{
    touchevent(0, 2, [self touchX:touches.anyObject], [self touchY:touches.anyObject]);
}

- (void)touchesEnded:(NSSet<UITouch *> *)touches withEvent:(UIEvent *)event
{
    touchevent(0, 1, [self touchX:touches.anyObject], [self touchY:touches.anyObject]);
}

- (void)touchesCancelled:(NSSet<UITouch *> *)touches withEvent:(UIEvent *)event
{
    touchevent(0, 3, [self touchX:touches.anyObject], [self touchY:touches.anyObject]);
}

- (int)touchX:(UITouch*)touch
{
    return (int)([touch locationInView:self.view].x * [[UIScreen mainScreen] scale]);
}
- (int)touchY:(UITouch*)touch
{
    return (int)([touch locationInView:self.view].y * [[UIScreen mainScreen] scale]);
}

@end
