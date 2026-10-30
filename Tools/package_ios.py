from pathlib import Path
import plistlib,subprocess,os,zipfile,json,hashlib,shutil,re
O=Path('/Volumes/RailRestoreWork/Outputs');D=Path(__file__).resolve().parent.parent/'Delivery';D.mkdir(exist_ok=True)
assert (D/'CityLoop_FullV1_iOS.railmap').exists(), 'Run package_map.py first'
X=O/'Xcode'
startup=Path(__file__).with_name('NativeStartup.mm.txt').read_text()
for source in X.rglob('RailDocumentPicker.mm'):
 if 'RailMobileStartup' not in source.read_text():source.write_text(source.read_text()+startup)
project=X/'Unity-iPhone.xcodeproj/project.pbxproj'
pbx=project.read_text()
def arc(m):
 value=m.group(0)
 if 'COMPILER_FLAGS' not in value:value=value[:-2]+' settings = {COMPILER_FLAGS = "-fobjc-arc"; }; };'
 return value
pbx=re.sub(r'[A-F0-9]+ /\* RailDocumentPicker\.mm in Sources \*/ = \{[^\n]+?\};',arc,pbx)
if 'UniformTypeIdentifiers' not in pbx:pbx=pbx.replace('OTHER_LDFLAGS = (','OTHER_LDFLAGS = (\n\t\t\t\t\t"-framework",\n\t\t\t\t\t"UniformTypeIdentifiers",')
pbx=re.sub(r'^\s*[A-F0-9]+ /\* LaunchScreen-i(?:Phone|Pad)\.storyboard in Resources \*/,\n','',pbx,flags=re.M)
# Standalone PNG app icons do not require a simulator runtime/asset catalog compiler.
iconDir=X/'Unity-iPhone/Images.xcassets/AppIcon.appiconset'
icons=sorted(iconDir.glob('*.png'));assert len(icons)>=5
if 'RailStandaloneIcon' not in pbx:
 defs='';refs='';phase=''
 for i,icon in enumerate(icons):
  uid=hashlib.md5(('RailIconFile'+icon.name).encode()).hexdigest().upper();bid=hashlib.md5(('RailIconBuild'+icon.name).encode()).hexdigest().upper()
  defs+=f'\t{bid} /* RailStandaloneIcon in Resources */ = {{isa = PBXBuildFile; fileRef = {uid}; }};\n'
  refs+=f'\t{uid} /* RailStandaloneIcon */ = {{isa = PBXFileReference; lastKnownFileType = image.png; path = "Unity-iPhone/Images.xcassets/AppIcon.appiconset/{icon.name}"; sourceTree = SOURCE_ROOT; }};\n'
  phase+=f'\t\t\t\t{bid} /* RailStandaloneIcon in Resources */,\n'
 pbx=pbx.replace('/* End PBXBuildFile section */',defs+'/* End PBXBuildFile section */').replace('/* End PBXFileReference section */',refs+'/* End PBXFileReference section */')
 pbx=re.sub(r'^\s*[A-F0-9]+ /\* Images\.xcassets in Resources \*/,\n',phase,pbx,flags=re.M)
pbx=re.sub(r'^\s*ASSETCATALOG_COMPILER_APPICON_NAME = AppIcon;\n','',pbx,flags=re.M)
project.write_text(pbx)
p=X/'Info.plist';info=plistlib.loads(p.read_bytes());info['UIFileSharingEnabled']=True;info['LSSupportsOpeningDocumentsInPlace']=True
info['UTImportedTypeDeclarations']=[{'UTTypeIdentifier':'com.railindependent.railmap','UTTypeDescription':'Rail Simulator Map','UTTypeConformsTo':['public.data'],'UTTypeTagSpecification':{'public.filename-extension':['railmap']}}]
phone=[p.stem for p in icons if 'iPhone' in p.name];ipad=[p.stem for p in icons if 'iPad' in p.name]
info['CFBundleIcons']={'CFBundlePrimaryIcon':{'CFBundleIconFiles':phone,'UIPrerenderedIcon':False}}
info['CFBundleIcons~ipad']={'CFBundlePrimaryIcon':{'CFBundleIconFiles':ipad,'UIPrerenderedIcon':False}}
info['UILaunchScreen']={}
for key in list(info):
 if key.startswith('UILaunchStoryboardName') or key.startswith('UILaunchImages'):info.pop(key,None)
info.pop('CFBundleDocumentTypes',None) # Import via in-game Files picker; do not advertise an unimplemented Open-In handler.
p.write_bytes(plistlib.dumps(info))
# No signing credentials requested; SideStore re-signs the device IPA.
env=dict(os.environ,DEVELOPER_DIR='/Volumes/lkx/临时Xcode/Xcode.app/Contents/Developer',TMPDIR='/Volumes/RailRestoreWork/Tmp')
cmd=['/usr/bin/xcodebuild','-project',str(X/'Unity-iPhone.xcodeproj'),'-target','Unity-iPhone','-configuration','Release','-sdk','iphoneos','SYMROOT='+str(O/'DerivedData/Build/Products'),'OBJROOT='+str(O/'DerivedData/Build/Intermediates.noindex'),'SHARED_PRECOMPS_DIR='+str(O/'DerivedData/PrecompiledHeaders'),'CODE_SIGNING_ALLOWED=NO','CODE_SIGNING_REQUIRED=NO','CODE_SIGN_IDENTITY=','IPHONEOS_DEPLOYMENT_TARGET=17.0','ARCHS=arm64','ONLY_ACTIVE_ARCH=NO','build']
(D/'xcode_command.txt').write_text(' '.join(cmd))
with (D/'xcode_build.log').open('w') as log:
 result=subprocess.run(cmd,env=env,stdout=log,stderr=subprocess.STDOUT)
assert result.returncode==0,'Xcode build failed; see xcode_build.log'
apps=list((O/'DerivedData/Build/Products/Release-iphoneos').glob('*.app'));assert len(apps)==1
app=apps[0];privacy={'NSPrivacyTracking':False,'NSPrivacyCollectedDataTypes':[],'NSPrivacyAccessedAPITypes':[{'NSPrivacyAccessedAPIType':'NSPrivacyAccessedAPICategoryDiskSpace','NSPrivacyAccessedAPITypeReasons':['E174.1']}]};privacyFile=app/'PrivacyInfo.xcprivacy'
if privacyFile.exists():
 original=plistlib.loads(privacyFile.read_bytes());original.setdefault('NSPrivacyTracking',False);original.setdefault('NSPrivacyCollectedDataTypes',[]);entries=original.setdefault('NSPrivacyAccessedAPITypes',[])
 match=next((e for e in entries if e.get('NSPrivacyAccessedAPIType')=='NSPrivacyAccessedAPICategoryDiskSpace'),None)
 if match is None:entries.extend(privacy['NSPrivacyAccessedAPITypes'])
 elif 'E174.1' not in match['NSPrivacyAccessedAPITypeReasons']:match['NSPrivacyAccessedAPITypeReasons'].append('E174.1')
 privacy=original
privacyFile.write_bytes(plistlib.dumps(privacy))
# Make a conventional Payload/App.app IPA with permissions and symlinks retained.
payload=O/'Distribution/Payload';payload.mkdir(parents=True,exist_ok=True);dst=payload/app.name
if dst.exists():shutil.rmtree(dst)
subprocess.run(['/usr/bin/ditto',str(app),str(dst)],check=True)
ipa=D/'RailDrivingSimulator_iOS_1.0.4_Restored.ipa'
subprocess.run(['/usr/bin/zip','-q','-r','-y',str(ipa),'Payload'],cwd=payload.parent,check=True)
with zipfile.ZipFile(ipa) as z:
 assert z.testzip() is None
 names=z.namelist();assert any(n.endswith('/Info.plist') for n in names)
 assert not any('/StreamingAssets/Maps/' in n or '/Data/Raw/Maps/' in n or n.endswith('.railmap') for n in names)
print('IOS_IPA_PACKAGE_PASS arm64=true no_bundled_map=true archive_crc=true',flush=True)
