<?php
defined('_JEXEC') or die('Restricted access');
//JHtml::_('behavior.tooltip');
//JHtml::_('behavior.formvalidation');
$searchid = JRequest::getVar('searchid') ;
$afteredit = $this->afteredit;
$ae_keys = array_keys($afteredit);

$db =& JFactory::getDBO();

if ($afteredit['plateid'] == "") $afteredit['plateid'] = $afteredit['title'];
$plateids = split(",", $afteredit['plateid']);
$submit = $afteredit['Submit'];

$columns = "";
$vcolumn = "";
$addtoquery = " ";

$uploadsfolder = "/srv/www/htdocs/uploads/";
$fileresult = "";
$layout_filename = basename($_FILES['uploadedfile']['name']);
if (empty($layout_filename)) {
  $fileresult = "(No layout file was supplied)";
} else if ($submit == 'Save') {
  $target_path = $uploadsfolder . $layout_filename;
  if (file_exists($target_path)) {
    $fileresult = $layout_filename . " has already been uploaded.";
    $newname = $layout_filename . "_v2";
    $version = 2;
    while (file_exists($uploadsfolder . $newname)) {
      $version += 1;
      $newname = $layout_filename . "_v" + $version;
    }
    $fileresult = $layout_filename . " has already been uploaded. Will be stored as " . $newname;
    $layout_filename = $newname;
    $target_path = $uploadsfolder . $layout_filename;
  }
  if (!move_uploaded_file($_FILES['uploadedfile']['tmp_name'], $target_path)) {
    $fileresult = "Error uploading $layoutfilname";
  } else {
    $fileok = checkFormat(file($target_path));
    if ($fileok != "OK") {
      unlink($target_path);
      JError::raiseWarning('Message', JText::_("Layout file has errors - please correct and upload again!<br/>" . $fileok));
      $fileresult = "Layout file had error(s): " . $fileok;
    } else {
      $fileresult = $layout_filename . " was uploaded.";
      $addtoquery = " , layoutfile=" . $db->Quote($layout_filename) . " ";
      $columns .= " layoutfile, ";
      $vcolumn .= $db->Quote($layout_filename) . ", ";
    }
  }
}

//  M.W. of dsDNA = (# nucleotides x 607.4) + 157.9
#    $MW = ($nts * 607.4) + 157.9;
#    $mlc = $wgt * 1000000 / $MW;
  
if ($submit != 'Save') {
  JError::raiseNotice('Message', JText::_('Cancel - no actions!'));
} else {

  $newquery = " INSERT INTO #__aaaproject (";
  $query = " UPDATE #__aaaproject SET ";
  echo "<table>";
  foreach ($afteredit as $key => $value) {
    echo "<tr><td>" . $key . "</td><td>" . $value . "</td></tr>";
    if (($key == 'platereference') || ($key == 'barcodeset') || ($key == 'species') || ($key == 'tissue')
        || ($key == 'sampletype') || ($key == 'collectionmethod') || ($key == 'weightconcentration')
        || ($key == 'fragmentlength') || ($key == 'molarconcentration') || ($key == 'labbookpage')
        || ($key == 'protocol') || ($key == 'comment') || ($key == 'user') 
        || ($key == 'time') || ($key == '#__aaamanagerid')
        || ($key == '#__aaacontactid') || ($key == '#__aaaclientid')) {
      $columns .= $key . ", ";
      $vcolumn .= $db->Quote($value) . ", ";
      $query .= $key . " = " . $db->Quote($value) . ", ";
    }
    if ($key == 'id') {
      $searchid = $value;
    }
  }
  echo "</table>";

  $bc = strtolower($afteredit['barcodeset']);
  if ($bc != "v1" && $bc != "v2" && $bc != "no")
    $fileresult .= " NOTE: You have given a non-standard barcode set. A definition file has to exist on the server.";

  foreach ($plateids AS $plateid) {
    $title = ($afteredit['title'] == "")? $plateid : $afteredit['title'];
    $updquery = $query . " hits='1', title=" . $db->Quote($title) . ", plateid=" 
                . $db->Quote($plateid) . $addtoquery . " WHERE id = '" . $searchid . "' ";
    $insquery = $newquery . "$columns title, plateid, hits) VALUES ($vcolumn " . $db->Quote($title) . ", "
                . $db->Quote($plateid) . ", '1' ) ";
    if ($searchid == 0) {
      $db->setQuery($insquery);
    } else {
      $db->setQuery($updquery);
    }
    if (! $db->query()) {
      JError::raiseWarning('Message', JText::_('Could not save record' . $plateid . '! ' . $fileresult));
    } else {
      JError::raiseNotice('Message', JText::_('Record ' . $plateid . ' was saved! ' . $fileresult));
      if ($afteredit['generatebatches']) {
        $bquery = " INSERT INTO #__aaasequencingbatch (";
        $bkeycols = "";
        $bvalcols = "";
        foreach ($afteredit as $key => $value) {
          if (($key == '#__aaasequencingprimerid') || ($key == 'plannednumberofcycles') ||
              ($key == 'indexprimerid') || ($key == 'plannedindexcycles') ||
              ($key == 'labbookpage') || ($key == 'invoice') || ($key == 'signed') ||
              ($key == 'batchcomment') || ($key == 'user') || ($key == 'time')) {
            $bkeycols .= ($key == 'batchcomment')? 'comment, ' : $key . ", ";
            $bvalcols .= $db->Quote($value) . ", ";
          }
        }
        $aaaprojectid = $db->insertid();
        $bquery .= $bkeycols ." hits, #__aaaprojectid, title, plannednumberoflanes) VALUES (" . $bvalcols . "1, " . $aaaprojectid . ", ";
        $bquery1 = $bquery . "'#1', 1) ";
        $db->setQuery($bquery1);
        if ($db->query()) {
          JError::raiseNotice('Message', JText::_('Batch #1 (1 lane) was saved! '));
        } else {
          JError::raiseWarning('Message', JText::_('Could not save batch #1 (1 lane)! '));
        }
        if ($afteredit['nbatches'] == "1+3") {
          $bquery13 = $bquery . "'#2', 3) ";
          $db->setQuery($bquery13);
          if ($db->query()) {
            JError::raiseNotice('Message', JText::_('Batch #2 (3 lane) was saved! '));
          } else {
            JError::raiseWarning('Message', JText::_('Could not save batch #2 (3 lanes)! '));
          }
        }
      }
    }
  }
}
echo $db->getErrorMsg();

$menus = &JSite::getMenu();
$menu  = $menus->getActive();
$itemid = $menu->id;
echo "<br /><a href=index.php?option=com_dbapp&view=projects&Itemid="
     . $itemid . ">Return to sample list</a><br />&nbsp;<br />";
?>
<?php

// Returns an array of all valid genome designations.
// $genomesLocation is the path to the folder where the genomes directory is located.
function getValidBuilds($genomesLocation) {
     $genomesDir = rtrim($genomesLocation, "/");
     $buildsGlob = $genomesDir . DS . "genomes" . DS . "*" . DS . "genome" . DS . "SilverBulletGenes_*.txt";
     //$buildsGlob = $genomesDir . "\\" . "genomes\\*\\genome\\SilverBulletGenes_*.txt"; // Windows version
     $annotPaths = glob($buildsGlob);
     $matchPath = "/genomes.(.+).genome.SilverBulletGenes_(.+)\.txt/";
     $validBuilds = array("empty" => 1);
     foreach ($annotPaths as $annotPath) {
         if (preg_match($matchPath, $annotPath, $matches)) {
             $genome = $matches[1];
             $annotation = $matches[2];
             $validBuilds[$genome] = 1;
             $validBuilds[$genome . '_' .$annotation] = 1; // To allow to specify annotation versions.
             if (preg_match("/^mm[0-9]+$/", $genome)) {
                 $validBuilds["mm"] = 1;
                 $validBuilds["mouse"] = 1;
             }
             if (preg_match("/^hg[0-9]+$/", $genome)) {
                 $validBuilds["hs"] = 1;
                 $validBuilds["human"] = 1;
             }
         }
     }
     $validBuilds = array_keys($validBuilds);
     sort($validBuilds);
     return $validBuilds;
}

// Will return "OK" if file content is correctly formatted, otherwise an error message string.
// Use $validBuilds to allow additional builds (e.g. "dm3") apart from the standard species "mm", "hs", "gg", and "empty".
function checkFormat($fileContent, $validBuilds = array()) {
     $validSpecies = array();
     foreach (getValidBuilds("/data") as $validBuild)
         $validSpecies[] = strtolower($validBuild);
     $validSpecies[] = "chicken";
     if (stripos($fileContent[0], "SampleID", 0) != 0)
         return "Layout file is not in correct format (tab-delimited text) or does not have a valid header";
     $lines = $fileContent ;//explode("\n", trim($fileContent));
     $headers = explode("\t", $lines[0]);
     if (strtolower($headers[1]) != "species")
         return "Sample layout file does not have Species as second column of header";
     unset($lines[0]);
     $lineNo = 2;
      foreach ($lines as $line) {
         $fields = explode("\t", $line);
         if (count($fields) < 2)
             return "Sample layout has too few columns at line " . $lineNo;
         if (count($fields) > count($headers))
             return "Sample layout has too many columns at line " . $lineNo;
         $sp = strtolower(trim($fields[1]));
         if (!in_array($sp, $validSpecies))
         {
             $spString = implode(",", $validSpecies);
             return "Unknown species at line " . $lineNo . ": " . $fields[1] . " Allowed are: " . $spString;
         }
         $lineNo++;
     }
     return "OK";
}

?>

