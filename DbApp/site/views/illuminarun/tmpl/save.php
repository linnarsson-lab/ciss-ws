<?php
defined('_JEXEC') or die('Restricted access');
//JHtml::_('behavior.tooltip');
//JHtml::_('behavior.formvalidation');
  $searchid = JRequest::getVar('searchid') ;
  $afteredit = $this->afteredit;
  $ae_keys = array_keys($afteredit);

    $db =& JFactory::getDBO();

//  if ($searchid == 0) {
    $newquery = " INSERT INTO #__aaailluminarun ( ";
    $columns = "";
    $vcolumn = "";
    $query = " UPDATE #__aaailluminarun SET ";

//   file upload and move to specific place
$target_path = "/srv/www/htdocs/uploads/";
if (!empty($_FILES['uploadedfile']['name'])) {
  $target_path = $target_path . basename($_FILES['uploadedfile']['name']); 
} else {
  $target_path = $target_path . "ingenfil"; 
  $fileresult = "No file name given";
}
$showoutput = "";
$addtoquery = " ";
if (file_exists($target_path)) {
  $fileresult = "The file ".  basename($_FILES['uploadedfile']['name']) . " has already been uploaded.";
} else {
  if(move_uploaded_file($_FILES['uploadedfile']['tmp_name'], $target_path)) {
    $uploadok = 1;
    $fileresult = "The file ".  basename($_FILES['uploadedfile']['name']) . " has been uploaded";
    $addtoquery = " , rundocument = " . $db->Quote(basename( $_FILES['uploadedfile']['name'])) . " ";
    $columns .= " rundocument, ";
    $vcolumn .= $db->Quote(basename($_FILES['uploadedfile']['name'])) . ", ";
    $showoutput .= "<tr><td>rundocument</td><td>" . basename( $_FILES['uploadedfile']['name']) . "</td></tr>";
  } else {
    $uploadok = -1;
    if (empty($_FILES['uploadedfile']['name'])) {
      $fileresult = "No file name given";
    } else {
      $fileresult = "There was an error uploading the file, please try again!";
    }
  }
}

  echo "<table>$showoutput";
  foreach ($afteredit as $key => $value) {
    echo "<tr><td>" . $key . "</td><td>" . $value . "</td></tr>";
    if (($key == 'illuminarunid') || ($key == 'rundate') || ($key == 'title') || ($key == 'labbookpage')
        || ($key == 'comment') || ($key == 'user') || ($key == 'time') || ($key == 'cycles')
        || ($key == 'indexcycles') || ($key == 'pairedcycles')) {
      $columns .= $key . ", ";
      $vcolumn .= $db->Quote($value) . ", ";
      $query .= $key . " = " . $db->Quote($value) . ", ";
    }
    if ($key == 'id') {
      $searchid = $value;
    }
    if ($key == 'Submit') {
      $submit = $value;
    }
  }
  echo "</table>";
//  echo "<H1>" . $afteredit->principalinvestigator . " &nbsp; &nbsp; &nbsp; &nbsp; id:" . $afteredit->id . "</H1><BR />";
    $query .= " hits = '1' $addtoquery WHERE id = '" . $searchid . "' ";
    $newquery .= $columns .  " hits) VALUES ( " . $vcolumn . " '1' ) "; 
    

     if ($searchid == 0) {
       $db->setQuery($newquery);
     } else {
       $db->setQuery($query);
     }

       if ($submit == 'Save') {
         if ($db->query()) {
           JError::raiseNotice('Message', JText::_('The record was saved! ' . $fileresult));
           /* lanes inserted automatically by a TRIGGER instead of below code
             if ($searchid == 0) {
             $searchid = $db->insertid();
             for ($i = 1; $i <= 8; $i++) {
               $lQuery = " INSERT INTO #__aaalane (#__aaailluminarunid, laneno) VALUES ('$searchid', '$i') ";
               $db->setQuery($lQuery);
               if ($db->query()) {
                 JError::raiseNotice('Message', JText::_('Added lane' . $i));
               } else {
                 JError::raiseWarning('Message', JText::_('Could not add lane' . $i));
               }
             }
           }*/
//          echo JText::_('<br/>query ok<br/>');
         } else {
           JError::raiseWarning('Message', JText::_('Could not save record! ' . $fileresult));
         }
       } else {
         JError::raiseNotice('Message', JText::_('Cancel - no actions! ' . $fileresult));
       }
//    }
 
//    $aearr = array($afteredit);
  //  $db->updateObject('#__aaaclient', &$aearr, 'id');
    echo $db->getErrorMsg();

    $menus = &JSite::getMenu();
    $menu  = $menus->getActive();
    $itemid = $menu->id;
    echo "<br /><a href=index.php?option=com_dbapp&view=illuminaruns&Itemid=" . $itemid . ">Return to Illumina runs list</a><br />&nbsp;<br />";
?>


