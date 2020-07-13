// methods exported by Unity,Tiny.IOS package

extern "C" {
void init(void *nwh, int width, int height, int orientation);
void set_viewcontroller(UIViewController* viewController);
void step(double timestamp);
void pauseapp(int paused);
void destroyapp(void);
void startapp(void);
void touchevent(int id, int action, int xpos, int ypos);
void deviceOrientationChanged(int orientation);
}
