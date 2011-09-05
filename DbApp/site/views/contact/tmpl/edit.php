<?php
defined('_JEXEC') or die('Restricted access');
JHtml::_('behavior.tooltip');
JHtml::_('behavior.formvalidation');
  $searchid = JRequest::getVar('searchid') ;
  $contact = $this->contact;

?>

<form action="<?php echo JText::_('?option=com_dbapp&view=contact&layout=save&id='.(int) $searchid); ?>" method="post" name="adminForm" id="admin-form" class="form-validate">
<H1>Contact - Edit VIEW</H1>
<div class='contact'><fieldset><legend>
<?php
  if ($searchid > 0) {
    echo " Edit contact record &nbsp; &nbsp; &nbsp; &nbsp; DB id: '" . $contact->id . "' ";
  } else {
    echo " Add new contact &nbsp; &nbsp; &nbsp; ";
  }

?>
</legend>
<table>
<tr><th>Contact&nbsp;person&nbsp;</th><td>
<input type="text" name="contactperson" id="contactperson" value="<?php if ($searchid > 0) echo $contact->contactperson; ?>" class="inputbox required" size="40"/></td></tr>
<tr><th>Email&nbsp;</th><td>
<input type="text" name="contactemail" id="contactemail" value="<?php if ($searchid > 0) echo $contact->contactemail; ?>" class="inputbox required" size="40"/></td></tr>
<tr><th>Phone&nbsp;no&nbsp;</th><td>
<input type="text" name="contactphone" id="contactphone" value="<?php if ($searchid > 0) echo $contact->contactphone; ?>" class="inputbox required" size="40"/></td></tr>
<tr><th>Comment&nbsp;</th><td>
<input type="text" name="comment" id="comment" value="<?php if ($searchid > 0) echo $contact->comment; ?>" class="inputbox" size="40"/></td></tr>
<?php
    $user =& JFactory::getUser();
    date_default_timezone_set('Europe/Stockholm');
    $today = date("Y-m-d H:i:s");
 if ($searchid > 0) {
    echo "<tr><td>User: " . $contact->user . "</td><td>";
    echo "Creation&nbsp;date: " . $contact->time . "</td></tr>";
    echo "<tr><td colspan='2'>Will be replaced by</td></tr>";
    echo "<tr><td>User: " . $user->username . "</td><td>";
    echo "Creation&nbsp;date: " . $today . "</td></tr>";
 } else {
    echo "<tr><td>User: " . $user->username . "</td><td>";
    echo "Creation&nbsp;date: " . $today . "</td></tr>";

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
    echo "<br /><a href=index.php?option=com_dbapp&view=contacts&Itemid=" . $itemid . ">Return to contacts list</a><br />&nbsp;<br />";

    echo '<input type="hidden" name="user" value="' . $user->username . '" />';
    echo '<input type="hidden" name="time" value="' . $today . '" />';
    if ($searchid == 0) {
    } else {
      echo '<input type="hidden" name="id" value="' . $contact->id . '" />';
    }

    echo '<input type="hidden" name="Itemid" value="' . $itemid . '" />';

?>
		<input type="hidden" name="task" value="save" />
		<?php echo JHtml::_('form.token'); ?>
</form>






