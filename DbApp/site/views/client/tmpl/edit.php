<?php
defined('_JEXEC') or die('Restricted access');
JHtml::_('behavior.tooltip');
JHtml::_('behavior.formvalidation');
  $searchid = JRequest::getVar('searchid') ;
  $client = $this->client;

?>

<form action="<?php echo JText::_('?option=com_dbapp&view=client&layout=save&id='.(int) $searchid); ?>" method="post" name="adminForm" id="admin-form" class="form-validate">
<h1><?php if ($searchid > 0) echo "Edit"; else echo "New";?> client</h1>
<div class='client'><fieldset><legend>
<?php
  if ($searchid > 0) echo "[ DBId: $client->id ]";
  $db =& JFactory::getDBO();
  $query = " SELECT id, path FROM #__categories WHERE extension = 'com_dbapp' ";
  $db->setQuery($query);
  $messages = $db->loadObjectList();
?>
</legend>
<table>
  <tr><th>Principal&nbsp;investigator&nbsp;</th><td>
    <input type="text" name="principalinvestigator" id="principalinvestigator" value="<?php if ($searchid > 0) echo $client->principalinvestigator; ?>" class="inputbox required" size="40"/></th></tr>
  <tr><th>Department&nbsp;</th><td>
    <input type="text" name="department" id="department" value="<?php if ($searchid > 0) echo $client->department; ?>" class="inputbox required" size="40"/></td></tr>
  <tr><th>Category&nbsp;</th><td>
    <select name="catid" id="catid" >
      <option value="0">choose an option</option>
<?php foreach ($messages as $message) : ?>
      <option value="<?php echo $message->id; ?>" <?php if ($searchid > 0)  { if ($message->id == $client->catid) echo ' selected = "selected" '; } ?> ><?php echo $message->path; ?></option>
<?php endforeach; ?>
    </select></td></tr>
  <tr><th>Address&nbsp;</th><td>
     <input type="text" name="address" id="address" value="<?php if ($searchid > 0) echo $client->address; ?>" class="inputbox required" size="40"/></td></tr>
  <tr><th>Vat&nbsp;No&nbsp;</th><td>
    <input type="text" name="vatno" id="vatno" value="<?php if ($searchid > 0) echo $client->vatno; ?>" class="inputbox" size="40"/></td></tr>
  <tr><th>Comment&nbsp;</th><td>
    <input type="text" name="comment" id="comment" value="<?php if ($searchid > 0) echo $client->comment; ?>" class="inputbox" size="40"/></td></tr>
<?php
    $user =& JFactory::getUser();
    date_default_timezone_set('Europe/Stockholm');
    $today = date("Y-m-d H:i:s");
  if ($searchid > 0) {
    echo "<tr><td>User: " . $client->user . "</td><td>Creation&nbsp;date: " . $client->time . "</td></tr>";
    echo "<tr><td colspan='2'>Will be replaced by</td></tr>";
  }
  echo "<tr><td>User: $user->username </td><td>Creation&nbsp;date: $today </td></tr>";
?>
</table>
</fieldset></div>
<br />
<input type="Submit" name="Submit" value="Save">
<input type="Submit" name="Submit" value="Cancel" >
<?php
    $menus = &JSite::getMenu();
    $menu  = $menus->getActive();
    $itemid = $menu->id;
    echo "<a href=\"index.php?option=com_dbapp&view=clients&Itemid=" . $itemid . "\">Return to client list</a>";
    echo '<input type="hidden" name="user" value="' . $user->username . '" />';
    echo '<input type="hidden" name="time" value="' . $today . '" />';
    if ($searchid != 0) {
      echo '<input type="hidden" name="id" value="' . $client->id . '" />';
    }
?>
    <input type="hidden" name="task" value="save" />
    <?php echo JHtml::_('form.token'); ?>
</form>

