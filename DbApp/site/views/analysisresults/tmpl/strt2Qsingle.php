<?php 
/* Run the php script using
$ php test.php */

function toQlucore ($infilePath) {

  $infileName = basename($infilePath);
  $tmpPath = "/srv/www/htdocs/joomla16/tmp/";
  $outfileName = substr($infileName, 0, strrpos($infileName, ".")) . ".gedata";
  $outfilePath = $tmpPath . "/" . $outfileName;
  print "<br />Will parse $infilePath for Qlucore.\n";
  $dirs = explode("/", $filename);
  $noPath = $dirs[count($dirs) - 1];
  $noPathArray = explode("_", $noPath);
  $lNo = $noPathArray[0];

$samples = array();
$lines = file($infilePath);
$count = 0;
$preresults = 1;
$sampleAnnotationIds = array();
$sampleAnnotations = array();
$variableAnnotationIds = array("Feature", "Chr", "Pos", "Strand", "TrLen");
$variableAnnotations = array();
$variables = array();
$genes = array();
foreach($lines as $line) {
//print $preresults;
  $count++;
//  echo($line);
  $words = explode("\t", $line);
  if (count($words) > 7) {
//    if ($count > 200) break;
    if ($preresults) {
      if (preg_match("/:$/", $words[7])) $annotidx = 7;
      else if (preg_match("/:$/", $words[9])) $annotidx = 9;
      if ($annotidx > 0) {
        if ($words[$annotidx] == "Sample:") {
          for ($cnt = $annotidx + 1; $cnt < count($words); $cnt++) {
            array_push($samples, $lNo . "_" . trim($words[$cnt]));
          }
        } else {
          $sampleAnnotationId = str_replace(":", "", $words[$annotidx]);
          if (strlen(trim($sampleAnnotationId)) > 0) {
            array_push($sampleAnnotationIds, $sampleAnnotationId);
            $sampleAnnotations[$sampleAnnotationId] = array();
            for ($cnt = $annotidx + 1; $cnt < count($words); $cnt++) {
              array_push($sampleAnnotations[$sampleAnnotationId], trim($words[$cnt]));
            }
          }
        }
//        print count($samples) . $samples[45] . $words[9] . $fet;
      }
    } else if ($words[0] != "Normalizer" && $words[0] != "SingleRead") {
//      print ("$count ");
      $variableAnnotations[$words[0]] = array();
      for ($cnt = 0; $cnt < 5; $cnt++) {
        array_push($variableAnnotations[$words[0]], $words[$cnt]);
      }
      $variables[$words[0]] = array();
      for ($cnt = $annotIdx + 1; $cnt < count($words); $cnt++) {
        array_push($variables[$words[0]], trim($words[$cnt]));
      }

      
    }
    if ($words[0] == "Feature")   $preresults = 0;
  }

//print $genes[count($genes) - 1];

}

$sampleCount = count($samples);
// foreach ($samples as $smp) {
//  print "$smp ";
// }
// print "\n";
$attributeCount = count($sampleAnnotationIds) + 1;
// foreach ($sampleAnnotationIds as $sai) {
//   print "$sai ";
// }
// print "\n";
$variableCount = count($variables);
// foreach($variables as $var => $ar) {
//   print "$var ";
// }
// print "\n";
$variableAnnotationCount = count($variableAnnotationIds);
// foreach($variableAnnotationIds as $vai) {
//   print "$vai ";
// }
// print "\n";

// print "            sampleCount $sampleCount\n";
// print "         attributeCount $attributeCount\n";
// print "          variableCount $variableCount\n";
// print "variableAnnotationCount $variableAnnotationCount\n";

$fh = fopen($outfilePath, 'w') or die("Can't open file for writing");
// print header
fwrite($fh, "Qlucore\tgedata\tversion 1.0\n");
fwrite($fh, "\n");
fwrite($fh, "$sampleCount\tsamples\twith\t$attributeCount\tattributes\n");
fwrite($fh, "$variableCount\tvariables\twith\t$variableAnnotationCount\tannotations\n");
fwrite($fh, "\n");
// print sample annotations
for ($cnt = 0; $cnt < $variableAnnotationCount; $cnt++) {
  fwrite($fh, "\t");
}
fwrite($fh, "Sample");
foreach ($samples as $smp) {
  fwrite($fh, "\t$smp");
}
fwrite($fh, "\n");
foreach ($sampleAnnotations as $sai => $annots) {
  for ($cnt = 0; $cnt < $variableAnnotationCount; $cnt++) {
    fwrite($fh, "\t");
  }
  fwrite($fh, $sai);
  foreach ($annots as $annot) {
    fwrite($fh, "\t$annot");
  }
  fwrite($fh, "\n");
}
// print variableAnnotationIds
foreach ($variableAnnotationIds as $vai) {
  fwrite($fh, "$vai\t");
}
fwrite($fh, "\n");
// print features and values per sample
foreach ($variables as $var => $arr) {
  foreach ($variableAnnotations[$var] as $ann) {
    fwrite($fh, "$ann\t");
  }
  foreach ($variables[$var] as $var) {
    fwrite($fh, "\t$var");
  }
  fwrite($fh, "\n");
}
fclose($fh);

return($outfileName);
}


?>


