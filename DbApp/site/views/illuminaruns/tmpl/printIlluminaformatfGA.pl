#/usr/bin/perl -w

use strict;
use warnings;

unless (scalar(@ARGV) == 1) {
  print ="\n\tUsage: $0 <brlmm-p> \n\n\t Will create a new file with values accepted by GenAbel\n";
  exit(1);
} else {
  print "\n\t Will create a new file with values accepted by GenAbel\n";
}

open (IN, $ARGV