use Cwd;
use Cwd 'abs_path';
use Getopt::Long;
use File::Basename;
use File::Path;

my $monoroot = File::Spec->rel2abs(dirname(__FILE__) . "/../..");
my $monoroot = abs_path($monoroot);
my $buildScriptsRoot = "$monoroot/external/buildscripts";

my $build = 1;
my $clean = 1;
my $disableNormalProfile = 1;

# Handy troubleshooting/niche options

# The prefix hack probably isn't needed anymore.  Let's disable it by default and see how things go
my $shortPrefix = 1;

GetOptions(
   "build=i"=>\$build,
   "clean=i"=>\$clean,
   'disablenormalprofile=i'=>\$disableNormalProfile,
   'shortprefix=i'=>\$shortPrefix,
) or die ("illegal cmdline options");

system(
	"perl",
	"$buildScriptsRoot/build.pl",
	"--build=$build",
	"--clean=$clean",
	"--artifact=1",
	"--artifactscommon=1",
	"--artifactsruntime=0",
#	"--aotprofile=mobile_static",
#	"--aotprofiledestname=unity_aot",
	"--forcedefaultbuilddeps=1",
	"--shortprefix=$shortPrefix",
	"--disablenormalprofile=$disableNormalProfile") eq 0 or die ("Failed building aot profile\n");
