<?php // no direct access
defined('_JEXEC') or die('Restricted access'); ?>
<?php 
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $searchid = JRequest::getVar('searchid') ;
  $manager = $this->manager;
  $editlink = "<a href=index.php?option=com_dbapp&view=manager&layout=edit&controller=manager&searchid=" 
           . $manager->id . "&Itemid=" . $itemid . ">Edit this record</a>";
    echo "<H1>Manager - single record VIEW</H1>";
    echo "<div class='manager'><fieldset><legend>Manager &nbsp; '" . $manager->person . "' &nbsp; &nbsp; &nbsp; &nbsp; DB id: '" . $manager->id . "' &nbsp; &nbsp; &nbsp; " . $editlink . "</legend>";
    echo "<p> Manager is the person running the sequencing reactions/handling the Illumina machine  </p>";
    echo "<table><tr><th>Email:&nbsp;</th><td>" . $manager->email . "</td></tr>";
    echo "<tr><th>Phone&nbsp;no:&nbsp;</th><td>" . $manager->phone . "</td></tr>";
    echo "<tr><th>Comment:&nbsp;</th><td>" . $manager->comment . "</td></tr>";
    echo "<tr><th>User:&nbsp;</th><td>" . $manager->user . "";
    echo "<tr><th>Latest&nbsp;edit:&nbsp;</th><td>" . $manager->time . "</td></tr></table>";

    echo "<br />";
    echo "<a href=index.php?option=com_dbapp&view=managers&Itemid=" . $itemid . ">Return to list</a>";
?>

