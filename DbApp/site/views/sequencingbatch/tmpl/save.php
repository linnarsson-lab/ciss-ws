<?php
defined('_JEXEC') or die('Restricted access');
//JHtml::_('behavior.tooltip');
//JHtml::_('behavior.formvalidation');
  $searchid = JRequest::getVar('searchid') ;
  $afteredit = $this->afteredit;
  $ae_keys = array_keys($afteredit);

  $db =& JFactory::getDBO();

  if ($searchid == 0) {
    $newquery = " INSERT INTO #__aaasequencingbatch (";
    $columns = "";
    $vcolumn = "";
    $query = " UPDATE #__aaasequencingbatch SET ";
    
  echo "<table>";
  foreach ($afteredit as $key => $value) {
    echo "<tr><td>" . $key . "</td><td>" . $value . "</td></tr>";
    if (($key == 'plannednumberoflanes') || ($key == 'title') || ($key == 'indexprimerid') || ($key == 'labbookpage') || ($key == 'plannednumberofcycles') || ($key == 'plannedindexcycles') || ($key == 'cost') || ($key == 'invoice') || ($key == 'signed') || ($key == 'comment') || ($key == 'user') || ($key == 'time') || ($key == '#__aaaprojectid') || ($key == '#__aaasequencingprimerid')) {
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
    $query .= " hits = '1' WHERE id = '" . $searchid . "' ";
    $newquery .= $columns .  " hits) VALUES ( " . $vcolumn . " '1' ) "; 
    
     if ($searchid == 0) {
       $db->setQuery($newquery);
     } else {
       $db->setQuery($query);
     }

       if ($submit == 'Save') {
         if ($db->query()) {
           JError::raiseWarning('Message', JText::_('The record was saved!'));
         } else {
           JError::raiseWarning('Message', JText::_('Could not save record!'));
         }
       } else {
         JError::raiseWarning('Message', JText::_('Cancel - no actions!'));
       }
    }
    echo $db->getErrorMsg();

    $menus = &JSite::getMenu();
    $menu  = $menus->getActive();
    $itemid = $menu->id;
    echo "<br /><a href=index.php?option=com_dbapp&view=sequencingbatches&Itemid=" . $itemid . ">Return to sequencing batches list</a><br />&nbsp;<br />";
?>

