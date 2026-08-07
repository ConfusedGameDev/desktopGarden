// PollenGardenWindow — native window control for the Pollen Garden desktop overlay.
//
// Compiled into PollenGardenWindow.bundle (see build.sh next to this file) and called from
// MacWindowPlatform.cs via [DllImport("PollenGardenWindow")]. Plain C exports, no Swift runtime.
//
// Every entry point hops to the main thread: AppKit is main-thread-only, and Unity script
// callbacks are not guaranteed to arrive there.

#import <Cocoa/Cocoa.h>
#import <QuartzCore/QuartzCore.h>

static NSWindow *PGMainWindow(void)
{
    NSApplication *app = [NSApplication sharedApplication];
    NSWindow *window = app.mainWindow ?: app.keyWindow;
    if (window == nil && app.windows.count > 0)
    {
        window = app.windows.firstObject;
    }
    return window;
}

static void PGOnMain(void (^block)(void))
{
    if ([NSThread isMainThread])
    {
        block();
    }
    else
    {
        dispatch_async(dispatch_get_main_queue(), block);
    }
}

// Unity's content view either *is* a CAMetalLayer host or contains one; find it either way.
static CAMetalLayer *PGFindMetalLayer(CALayer *layer)
{
    if (layer == nil)
    {
        return nil;
    }
    if ([layer isKindOfClass:[CAMetalLayer class]])
    {
        return (CAMetalLayer *)layer;
    }
    for (CALayer *sublayer in layer.sublayers)
    {
        CAMetalLayer *found = PGFindMetalLayer(sublayer);
        if (found != nil)
        {
            return found;
        }
    }
    return nil;
}

// Last transparency state requested by C#. Kept so the styling can be re-applied after events
// that silently undo it — most importantly a window resize, after which Unity recreates its
// Metal surface with a fresh, opaque layer.
static bool gTransparent = false;

// Must run on the main thread.
static void PGApplyTransparency(void)
{
    NSWindow *window = PGMainWindow();
    if (window == nil)
    {
        return;
    }

    if (gTransparent)
    {
        // Borderless first: removing the title bar resizes the content view, and we want
        // the Metal layer already found and configured against the final view.
        window.styleMask = NSWindowStyleMaskBorderless;
        window.opaque = NO;
        window.backgroundColor = [NSColor clearColor];
        window.hasShadow = NO;
    }
    else
    {
        window.styleMask = NSWindowStyleMaskTitled
                         | NSWindowStyleMaskClosable
                         | NSWindowStyleMaskMiniaturizable
                         | NSWindowStyleMaskResizable;
        window.opaque = YES;
        window.backgroundColor = [NSColor windowBackgroundColor];
        window.hasShadow = YES;
    }

    NSView *view = window.contentView;
    view.wantsLayer = YES;
    view.layer.opaque = !gTransparent;
    view.layer.backgroundColor = gTransparent ? CGColorGetConstantColor(kCGColorClear) : NULL;

    CAMetalLayer *metalLayer = PGFindMetalLayer(view.layer);
    if (metalLayer != nil)
    {
        metalLayer.opaque = !gTransparent;
    }

    [window invalidateShadow];
}

// Sanity ping so the C# side can verify the bundle actually loaded.
bool PG_IsAvailable(void)
{
    return true;
}

void PG_SetTransparent(bool enabled)
{
    gTransparent = enabled;
    PGOnMain(^{
        PGApplyTransparency();
    });
}

void PG_SetClickThrough(bool enabled)
{
    PGOnMain(^{
        NSWindow *window = PGMainWindow();
        window.ignoresMouseEvents = enabled;
    });
}

void PG_SetAlwaysOnTop(bool enabled)
{
    PGOnMain(^{
        NSWindow *window = PGMainWindow();
        if (window == nil)
        {
            return;
        }

        if (enabled)
        {
            window.level = NSFloatingWindowLevel;
            window.collectionBehavior = NSWindowCollectionBehaviorCanJoinAllSpaces
                                      | NSWindowCollectionBehaviorFullScreenAuxiliary;
        }
        else
        {
            window.level = NSNormalWindowLevel;
            window.collectionBehavior = NSWindowCollectionBehaviorDefault;
        }
    });
}

// Primary screen's *visible* frame in points, origin bottom-left — excludes the menu bar and
// Dock. Deliberately not the full frame: a window covering the display exactly gets Metal's
// direct-to-display scanout, which bypasses window-server compositing (the transparent overlay
// renders over black instead of the desktop) and trips macOS Game Mode.
bool PG_TryGetScreenFrame(float *outX, float *outY, float *outWidth, float *outHeight)
{
    NSScreen *screen = NSScreen.screens.firstObject; // primary display owns the global origin
    if (screen == nil || outX == NULL || outY == NULL || outWidth == NULL || outHeight == NULL)
    {
        return false;
    }

    NSRect frame = screen.visibleFrame;
    *outX = (float)frame.origin.x;
    *outY = (float)frame.origin.y;
    *outWidth = (float)frame.size.width;
    *outHeight = (float)frame.size.height;
    return true;
}

// Cursor in window-local *pixels*, origin bottom-left — Unity's screen convention, so the C#
// side can compare it against WorldToScreenPoint rects with no further conversion.
//
// Deliberately not hopped to the main thread: this is polled every frame from Unity's script
// thread, and both NSEvent.mouseLocation and a frame read are safe enough for a point-in-rect
// test — worst case is a one-frame-stale answer, which the hysteresis absorbs anyway.
bool PG_TryGetCursorPixels(float *outX, float *outY)
{
    NSWindow *window = PGMainWindow();
    if (window == nil || outX == NULL || outY == NULL)
    {
        return false;
    }

    NSPoint global = [NSEvent mouseLocation];
    NSRect frame = window.frame; // borderless overlay: content rect == frame
    CGFloat scale = window.backingScaleFactor;

    *outX = (float)((global.x - frame.origin.x) * scale);
    *outY = (float)((global.y - frame.origin.y) * scale);
    return true;
}

// Screen points, macOS convention: origin bottom-left of the main screen.
//
// Resizing makes Unity rebuild its Metal surface (asynchronously, over the next few frames),
// and the fresh layer comes back opaque — silently killing the overlay's transparency. So the
// last-requested transparency is re-applied immediately and again shortly after, to catch the
// recreated layer whenever it lands.
void PG_SetWindowRect(float x, float y, float width, float height)
{
    PGOnMain(^{
        NSWindow *window = PGMainWindow();
        [window setFrame:NSMakeRect(x, y, width, height) display:YES];
        PGApplyTransparency();

        for (int64_t delayMs = 100; delayMs <= 800; delayMs *= 2)
        {
            dispatch_after(dispatch_time(DISPATCH_TIME_NOW, delayMs * NSEC_PER_MSEC),
                           dispatch_get_main_queue(), ^{
                PGApplyTransparency();
            });
        }
    });
}
