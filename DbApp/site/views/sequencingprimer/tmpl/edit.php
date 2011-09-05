<?php
defined('_JEXEC') or die('Restricted access');
JHtml::_('behavior.tooltip');
JHtml::_('behavior.formvalidation');
  $searchid = JRequest::getVar('searchid') ;
  $item = $this->item;
?>

<form action="<?php echo JText::_('?option=com_dbapp&view=sequencingprimer&layout=save&id='.(int) $searchid); ?>" method="post" name="adminForm" id="admin-form" class="form-validate">
<?php
  if ($searchid > 0) {
    echo "<h1> Edit sequencing primer: " . $item->primername . " </h1>";
  } else {
    echo "<h1> Add new sequencing primer </h1>";
  }
?>
<div class='sequencingprimer'>
  <fieldset>
    <legend></legend>
    <table>
    <tr>
      <th>Sequencing&nbsp;primer&nbsp;</th>
      <td><input type="text" name="primername" id="primername" value="<?php if ($searchid > 0) echo $item->primername; ?>" class="inputbox required" size="40"/></td>
    </tr>
    <tr>
      <th>Sequence&nbsp;</th>
      <td><input type="text" name="sequence" id="sequence" value="<?php if ($searchid > 0) echo $item->sequence; ?>" class="inputbox" size="40"/></td>
    </tr>
    <tr>
      <th>Comment&nbsp;</th>
      <td><input type="text" name="comment" id="comment" value="<?php if ($searchid > 0) echo $item->comment; ?>" class="inputbox" size="40"/></td>
    </tr>
<?php
    $user =& JFactory::getUser();
    date_default_timezone_set('Europe/Stockholm');
    $today = date("Y-m-d H:i:s");
 if ($searchid > 0) {
    echo "<tr><td>User: " . $item->user . "</td>";
    echo "    <td>Creation&nbsp;date: " . $item->time . "</td></tr>";
    echo "<tr><td colspan='2'>Will be replaced by</td></tr>";
    echo "<tr><td>User: " . $user->username . "</td>";
    echo "    <td>Creation&nbsp;date: " . $today . "</td></tr>";
 } else {
    echo "<tr><td>User: " . $user->username . "</td>";
    echo "    <td>Creation&nbsp;date: " . $today . "</td></tr>";

 }
?>
</table>
</fieldset></div>
<br/>
<input type="Submit" name="Submit" value="Save">
<input type="Submit" name="Submit" value="Cancel" >
<?php
    $menus = &JSite::getMenu();
    $menu  = $menus->getActive();
    $itemid = $menu->id;
    echo "&nbsp; &nbsp; &nbsp; <a href=index.php?option=com_dbapp&view=sequencingprimers&Itemid=" . $itemid . ">Return to sequencing primers list</a><br />&nbsp;<br />";

    echo '<input type="hidden" name="user" value="' . $user->username . '" />';
    echo '<input type="hidden" name="time" value="' . $today . '" />';
    if ($searchid == 0) {
    } else {
      echo '<input type="hidden" name="id" value="' . $item->id . '" />';
    }

?>
		<input type="hidden" name="task" value="save" />
		<?php echo JHtml::_('form.token'); ?>
</form>






