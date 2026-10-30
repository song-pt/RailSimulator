#import <UIKit/UIKit.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>
#include <sys/statvfs.h>
extern "C" void UnitySendMessage(const char*,const char*,const char*);
extern "C" UIViewController* UnityGetGLViewController(void);
extern "C" long long RailAvailableBytes();
@interface RailDocumentPicker:NSObject<UIDocumentPickerDelegate>
@property(nonatomic,copy)NSString*receiver;
@end
static RailDocumentPicker* retainedPicker;
static void Send(NSString*r,NSString*v){dispatch_async(dispatch_get_main_queue(),^{UnitySendMessage(r.UTF8String,"OnIOSMapPicked",v.UTF8String);});}
@implementation RailDocumentPicker
-(void)documentPickerWasCancelled:(UIDocumentPickerViewController*)controller{Send(self.receiver,@"");retainedPicker=nil;}
-(void)documentPicker:(UIDocumentPickerViewController*)controller didPickDocumentsAtURLs:(NSArray<NSURL*>*)urls{NSString*r=self.receiver;NSURL*u=urls.firstObject;if(!u){Send(r,@"");retainedPicker=nil;return;}NSNumber*isDirectory=nil;[u getResourceValue:&isDirectory forKey:NSURLIsDirectoryKey error:nil];if(isDirectory.boolValue){Send(r,@"ERROR:请选择 .railmap 地图文件，不要选择文件夹");retainedPicker=nil;return;}dispatch_async(dispatch_get_global_queue(QOS_CLASS_UTILITY,0),^{BOOL access=[u startAccessingSecurityScopedResource];NSFileCoordinator*c=[[NSFileCoordinator alloc]initWithFilePresenter:nil];NSError*coordError=nil;__block NSError*copyError=nil;NSString*dir=[NSTemporaryDirectory() stringByAppendingPathComponent:@"RailImport"];[[NSFileManager defaultManager]createDirectoryAtPath:dir withIntermediateDirectories:YES attributes:nil error:&copyError];NSString*dest=[dir stringByAppendingPathComponent:[NSUUID.UUID.UUIDString stringByAppendingString:@".railmap"]];[c coordinateReadingItemAtURL:u options:0 error:&coordError byAccessor:^(NSURL*url){NSNumber*size=nil;[url getResourceValue:&size forKey:NSURLFileSizeKey error:nil];if(size&&size.longLongValue>RailAvailableBytes()-268435456){copyError=[NSError errorWithDomain:@"RailImport" code:1 userInfo:@{NSLocalizedDescriptionKey:@"手机空间不足，请清理后导入"}];return;}[[NSFileManager defaultManager]copyItemAtURL:url toURL:[NSURL fileURLWithPath:dest] error:&copyError];}];if(access)[u stopAccessingSecurityScopedResource];NSError*e=coordError?:copyError;if(e){[[NSFileManager defaultManager]removeItemAtPath:dest error:nil];Send(r,[@"ERROR:" stringByAppendingString:e.localizedDescription]);}else{NSURL*url=[NSURL fileURLWithPath:dest];[url setResourceValue:@YES forKey:NSURLIsExcludedFromBackupKey error:nil];Send(r,dest);}dispatch_async(dispatch_get_main_queue(),^{retainedPicker=nil;});});}
@end
extern "C" void RailPickDocument(const char*receiver){NSString*r=[NSString stringWithUTF8String:receiver];dispatch_async(dispatch_get_main_queue(),^{retainedPicker=[RailDocumentPicker new];retainedPicker.receiver=r;UIDocumentPickerViewController*p=[[UIDocumentPickerViewController alloc]initForOpeningContentTypes:@[UTTypeData,UTTypeContent] asCopy:YES];p.shouldShowFileExtensions=YES;p.title=@"选择地图文件（.railmap）";p.delegate=retainedPicker;p.allowsMultipleSelection=NO;UIViewController*v=UnityGetGLViewController();while(v.presentedViewController)v=v.presentedViewController;[v presentViewController:p animated:YES completion:nil];});}
extern "C" long long RailAvailableBytes(){struct statvfs s;if(statvfs(NSHomeDirectory().fileSystemRepresentation,&s)!=0)return 0;return (long long)s.f_bavail*s.f_frsize;}

extern "C" void RailExcludeBackup(const char*path){NSURL*u=[NSURL fileURLWithPath:[NSString stringWithUTF8String:path]];[u setResourceValue:@YES forKey:NSURLIsExcludedFromBackupKey error:nil];}

extern "C" void RailSelectionHaptic(){static UISelectionFeedbackGenerator*g; if(!g)g=[UISelectionFeedbackGenerator new];[g selectionChanged];[g prepare];}

// Mobile-only startup housekeeping; installed map folders are never removed.
__attribute__((constructor)) static void RailMobileStartup(void){@autoreleasepool{
 dispatch_async(dispatch_get_main_queue(),^{UIApplication.sharedApplication.idleTimerDisabled=YES;[[NSNotificationCenter defaultCenter]addObserverForName:UIApplicationDidBecomeActiveNotification object:nil queue:NSOperationQueue.mainQueue usingBlock:^(NSNotification*n){UIApplication.sharedApplication.idleTimerDisabled=YES;}];});
 NSFileManager*fm=NSFileManager.defaultManager;NSString*maps=[NSHomeDirectory() stringByAppendingPathComponent:@"Documents/Maps"];
 NSMutableArray<NSString*>*stale=[NSMutableArray array];
 for(NSString*name in [fm contentsOfDirectoryAtPath:maps error:nil]){
  NSString*p=[maps stringByAppendingPathComponent:name];
  if([name hasPrefix:@"_import_"]){[stale addObject:p];continue;}
  if([name hasPrefix:@"_backup_"]){NSData*data=[NSData dataWithContentsOfFile:[p stringByAppendingPathComponent:@"manifest.json"]];if(!data||data.length>4000000)continue;id m=[NSJSONSerialization JSONObjectWithData:data options:0 error:nil];id ident=[m isKindOfClass:NSDictionary.class]?m[@"id"]:nil;if(![ident isKindOfClass:NSString.class]||![ident length]||[ident containsString:@"/"]||[ident containsString:@"\\"]||[ident containsString:@".."]||[ident hasPrefix:@"_"])continue;NSString*dest=[maps stringByAppendingPathComponent:ident];if(![fm fileExistsAtPath:dest])[fm moveItemAtPath:p toPath:dest error:nil];}
 }
 NSString*tmp=[NSTemporaryDirectory() stringByAppendingPathComponent:@"RailImport"];
 for(NSString*name in [fm contentsOfDirectoryAtPath:tmp error:nil])[stale addObject:[tmp stringByAppendingPathComponent:name]];
 dispatch_async(dispatch_get_global_queue(QOS_CLASS_UTILITY,0),^{for(NSString*p in stale)[NSFileManager.defaultManager removeItemAtPath:p error:nil];});
}}
