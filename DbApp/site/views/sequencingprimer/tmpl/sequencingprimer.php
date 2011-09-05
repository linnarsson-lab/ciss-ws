<?php // no direct access
defined('_JEXEC') or die('Restricted access'); ?>
<?php 
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $searchid = JRequest::getVar('searchid') ;
  $item = $this->item;
  $editlink = "<a href=index.php?option=com_dbapp&view=sequencingprimer&layout=edit&controller=sequencingprimer&searchid=" 
           . $item->id . "&Itemid=" . $itemid . ">Edit this record</a>";
  echo "<h1>Sequencing primer: $item->primername </h1>
    <div class='sequencingprimer'>
      <fieldset>
        <legend> $editlink </legend>
        <table>
          <tr><th>Primer&nbsp;name:&nbsp;</th><td>" . $item->primername . "&nbsp;</td></tr>";
    echo "<tr><th>Sequence:&nbsp;</th><td>" . $item->sequence . "&nbsp;</td></tr>";
    echo "<tr><th>Comment:&nbsp;</th><td>" . $item->comment . "</td></tr>";
    echo "<tr><th>User:&nbsp;</th><td>" . $item->user . "";
    echo "<tr><th>Latest&nbsp;edit:&nbsp;</th><td>" . $item->time . "</td></tr></table>";
    echo "<br />";
    echo "<a href=index.php?option=com_dbapp&view=sequencingprimers&Itemid=" . $itemid . ">Return to list</a>";
?>

