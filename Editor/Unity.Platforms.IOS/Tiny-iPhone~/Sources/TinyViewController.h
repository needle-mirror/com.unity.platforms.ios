#import <UIKit/UIKit.h>

@interface TinyView : UIView
{
    CADisplayLink* m_displayLink;
    BOOL m_updateWindow;
}

@property (nonatomic, readwrite) BOOL m_visible;
@property (nonatomic, readwrite) BOOL m_preventRemoveFromSuperview;

- (void)start;
- (void)stop;

@end

@interface TinyViewController : UIViewController

@end
