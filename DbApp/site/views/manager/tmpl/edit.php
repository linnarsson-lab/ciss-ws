<?php
defined('_JEXEC') or die('Restricted access');
JHtml::_('behavior.tooltip');
JHtml::_('behavior.formvalidation');
  $searchid = JRequest::getVar('searchid') ;
  $manager = $this->manager;

?>

<script type="text/javascript">
function validateForm()
{
	var filter = /^([a-zA-Z0-9_\.\-])+\@(([a-zA-Z0-9\-])+\.)+([a-zA-Z0-9]{2,4})+$/;
	var email = document.getElementById("email").value;
	if (filter.test(email))
		return true;
	alert("The email address is invalid!");
	return false;
}
</script>

<form action="<?php echo JText::_('?option=com_dbapp&view=manager&layout=save&id='.(int) $searchid); ?>" onsubmit="return validateForm();" method="post" name="adminForm" id="admin-form" class="form-validate">
<h1>Manager - Edit VIEW</h1>
<div class='manager'><fieldset><legend>
<?php
  if ($searchid > 0) {
    echo " Edit manager record &nbsp; &nbsp; &nbsp; &nbsp; DB id: '" . $manager->id . "' ";
  } else {
    echo " Add new manager &nbsp; &nbsp; &nbsp; ";
  }
?>
</legend>
<table>
<tr><th>Manager&nbsp;</th><td>
<input type="text" name="person" id="person" value="<?php if ($searchid > 0) echo $manager->person; ?>" class="inputbox required" size="40"/></td></tr>
<tr><th>Email&nbsp;</td><th>
<input type="text" name="email" id="email" value="<?php if ($searchid > 0) echo $manager->email; ?>" class="inputbox required validate-email" size="40"/></td></tr>
<tr><th>Phone&nbsp;no&nbsp;</th><td>
<input type="text" name="phone" id="phone" value="<?php if ($searchid > 0) echo $manager->phone; ?>" class="inputbox required" size="40"/></td></tr>
<tr><th>Comment&nbsp;</th><td>
<input type="text" name="comment" id="comment" value="<?php if ($searchid > 0) echo $manager->comment; ?>" class="inputbox" size="40"/></td></tr>
<?php
    $user =& JFactory::getUser();
    date_default_timezone_set('Europe/Stockholm');
    $today = date("Y-m-d H:i:s");
 if ($searchid > 0) {
    echo "<tr><td>User: " . $manager->user . "</td><td>";
    echo "Creation&nbsp;date: " . $manager->time . "</td></tr>";
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
    echo "<br /><a href=index.php?option=com_dbapp&view=managers&Itemid=" . $itemid . ">Return to managers list</a><br />&nbsp;<br />";

    echo '<input type="hidden" name="user" value="' . $user->username . '" />';
    echo '<input type="hidden" name="time" value="' . $today . '" />';
    if ($searchid == 0) {
    } else {
      echo '<input type="hidden" name="id" value="' . $manager->id . '" />';
    }


?>
		<input type="hidden" name="task" value="save" />
		<?php echo JHtml::_('form.token'); ?>
</form>






